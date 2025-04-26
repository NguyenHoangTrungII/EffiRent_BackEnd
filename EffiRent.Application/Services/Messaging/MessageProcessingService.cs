using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Domain.ViewModels.MaintainRequest;
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
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Messaging
{
    public class MessageProcessingService : IMessageProcessingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MessageProcessingService> _logger;

        public MessageProcessingService(
            IServiceScopeFactory scopeFactory,
            ILogger<MessageProcessingService> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs ea, string message)
        {
            try
            {
                var maintenanceMessage = JsonSerializer.Deserialize<RabbitMaintenanceMessage>(message);
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
    }
}
