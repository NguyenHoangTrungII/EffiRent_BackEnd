using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiRent.Application.Services.Rabbit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Messaging
{
    public class MessageProcessingService : IMessageProcessingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MessageProcessingService> _logger;
        private readonly IRabbitMQProducerService _rabbitMQProducer;


        private readonly string _maintenanceExchange = "maintenance_exchange";
        private readonly string _retryExchange = "retry_exchange";
        private readonly string _completionExchange = "completion_exchange";
        private readonly string _maintenanceQueue = "maintenance_queue";
        private readonly string _retryQueue = "retry_queue";
        private readonly string _completionQueue = "completion_queue";
        private readonly string _maintenanceRoutingKey = "maintenance_request";
        private readonly string _completionRoutingKey = "completion_request";

        public MessageProcessingService(
            IServiceScopeFactory scopeFactory,
            ILogger<MessageProcessingService> logger,
            IRabbitMQProducerService rabbitMQProducer)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rabbitMQProducer = rabbitMQProducer;
        }

        public async Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs ea, string message)
        {
            try
            {
                string unescapedJson = JsonSerializer.Deserialize<string>(message);

                var maintenanceMessage = JsonSerializer.Deserialize<RabbitMaintenanceMessage>(unescapedJson);
                if (maintenanceMessage?.request == null)
                {
                    _logger.LogWarning("Invalid or empty MaintenanceMessage: {Message}", message);
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogInformation("Received MaintenanceMessage with RequestId {RequestId}, FileUrls count: {FileCount}",
                    maintenanceMessage.request.requestId, maintenanceMessage.FileBase64?.Count ?? 0);

                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var command = new AssignTechnicianCommand(maintenanceMessage);
                var response = await mediator.Send(command);

                if (response.Succeeded)
                {

                    if (response.Data.Status == "Queued")
                    {
                        _logger.LogInformation("Request {RequestId} queued, requeuing to retry_queue", response.Data.requestId);
                        channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    }
                    else
                    {
                        channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to process request for RequestId {RequestId}", maintenanceMessage.request.requestId);
                    channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Message}", message);
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        }



        public async Task ProcessCompletedMessageAsync(IModel _channel, BasicDeliverEventArgs ea, string message)
        {
            try
            {
                //var body = ea.Body.ToArray();
                //var message = Encoding.UTF8.GetString(body);
                //_logger.LogInformation("Received completion event from {Queue}: {Message}", _completionQueue, message);

                string unescapedJson = JsonSerializer.Deserialize<string>(message);


                // Deserialize completion event để lấy thông tin (giả sử có TechnicianId)
                var completionData = JsonSerializer.Deserialize<CompletionEvent>(unescapedJson);
                _logger.LogInformation("Technician {TechnicianId} is now available", completionData?.TechnicianId);

                // Lấy một message từ retry_queue
                var result = _channel.BasicGet(_retryQueue, autoAck: false);
                if (result == null)
                {
                    _logger.LogInformation("No messages in {Queue} to process", _retryQueue);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                var retryMessage = Encoding.UTF8.GetString(result.Body.ToArray());
                _logger.LogInformation("Fetched message from {RetryQueue}: {Message}", _retryQueue, retryMessage);

                // Gửi message trở lại maintenance_queue
                await _rabbitMQProducer.PublishAsync(retryMessage, _maintenanceExchange, _maintenanceRoutingKey);
                _logger.LogInformation("Moved message from {RetryQueue} to {MaintenanceQueue}", _retryQueue, _maintenanceQueue);

                // Ack message từ retry_queue
                _channel.BasicAck(result.DeliveryTag, multiple: false);

                // Ack completion event
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing completion event from {Queue}", _completionQueue);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false); // Requeue completion event nếu lỗi
            }

        }

    }
}
