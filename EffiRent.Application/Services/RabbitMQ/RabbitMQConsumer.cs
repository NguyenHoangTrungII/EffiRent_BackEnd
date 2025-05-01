using EffiAP.Application.Services.Messaging;
using EffiRent.Application.Services.Messaging;

//using EffiRent.Application.Services.Messaging;
using EffiRent.Application.Services.Rabbit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IRabbitMQProducerService = EffiRent.Application.Services.Rabbit.IRabbitMQProducerService;

namespace EffiRent.Application.Services.Rabbit
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnection _connection;
        private readonly IModel _channel;


        private readonly string _maintenanceExchange = "maintenance_exchange";
        private readonly string _retryExchange = "retry_exchange";
        private readonly string _completionExchange = "completion_exchange";
        private readonly string _maintenanceQueue = "maintenance_queue";
        private readonly string _retryQueue = "retry_queue";
        private readonly string _completionQueue = "completion_queue";
        private readonly string _maintenanceRoutingKey = "maintenance_request";
        private readonly string _completionRoutingKey = "completion_request";
        private readonly int _retryTtl = 180000; // 3 phút

        public RabbitMQConsumer(
            ILogger<RabbitMQConsumer> logger,
            IServiceScopeFactory scopeFactory,
            IConnectionFactory connectionFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

            try
            {
                _connection = connectionFactory.CreateConnection();
                _channel = _connection.CreateModel();
                ConfigureRabbitMQ();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
                throw;
            }
        }

        private void ConfigureRabbitMQ()
        {
            _channel.ExchangeDeclare(_maintenanceExchange, ExchangeType.Direct, durable: true);
            _channel.ExchangeDeclare(_retryExchange, ExchangeType.Direct, durable: true);
            _channel.ExchangeDeclare(_completionExchange, ExchangeType.Direct, durable: true);

            var maintenanceQueueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", _retryExchange },
                { "x-dead-letter-routing-key", _maintenanceRoutingKey }
            };

            _channel.QueueDeclare(_maintenanceQueue, durable: true, exclusive: false, autoDelete: false, arguments: maintenanceQueueArgs);
            _channel.QueueBind(_maintenanceQueue, _maintenanceExchange, _maintenanceRoutingKey);

            var retryQueueArgs = new Dictionary<string, object>
            {
                { "x-message-ttl", _retryTtl },
                { "x-dead-letter-exchange", _maintenanceExchange },
                { "x-dead-letter-routing-key", _maintenanceRoutingKey }
            };

            _channel.QueueDeclare(_retryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryQueueArgs);
            _channel.QueueBind(_retryQueue, _retryExchange, _maintenanceRoutingKey);

            _channel.QueueDeclare(_completionQueue, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(_completionQueue, _completionExchange, _completionRoutingKey);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RabbitMQConsumer starting");

            try
            {
                // Consumer for maintenance queue
                /// <summary>
                /// Handles incoming messages from the maintenance queue.
                /// Processes each message using IMessageProcessingService.ProcessMessageAsync.
                /// </summary>
                var maintenanceConsumer = new AsyncEventingBasicConsumer(_channel);
                maintenanceConsumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    using var scope = _scopeFactory.CreateScope();
                    var messageProcessingService = scope.ServiceProvider.GetRequiredService<IMessageProcessingService>();
                    await messageProcessingService.ProcessMessageAsync(_channel, ea, message);
                };

                _channel.BasicConsume(queue: _maintenanceQueue, autoAck: false, consumer: maintenanceConsumer);
                _logger.LogInformation("Started consumer for {Queue}", _maintenanceQueue);

                // Consumer for completion queue
                /// <summary>
                /// Handles incoming messages from the completion queue.
                /// Processes each message using IMessageProcessingService.ProcessCompletedMessageAsync.
                /// </summary>
                var completionConsumer = new AsyncEventingBasicConsumer(_channel);
                completionConsumer.Received += async (model, ea) =>
                {

                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    using var scope = _scopeFactory.CreateScope();
                    var messageProcessingService = scope.ServiceProvider.GetRequiredService<IMessageProcessingService>();
                    await messageProcessingService.ProcessCompletedMessageAsync(_channel, ea, message);


                };

                _channel.BasicConsume(queue: _completionQueue, autoAck: false, consumer: completionConsumer);
                _logger.LogInformation("Started consumer for {Queue}", _completionQueue);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQConsumer error: {Error}", ex.Message);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RabbitMQConsumer stopping");
            _channel?.Close();
            _connection?.Close();
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing RabbitMQConsumer");
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}

//using EffiRent.Application.Services.Rabbit;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;

//namespace EffiAP.Application.Services.Rabbit
//{
//    public class RabbitMQConsumer : BackgroundService
//    {
//        private readonly ILogger<RabbitMQConsumer> _logger;
//        private readonly IServiceScopeFactory _scopeFactory;
//        private readonly IConnection _connection;
//        private readonly IModel _channel;

//        private readonly string _maintenanceExchange = "maintenance_exchange";
//        private readonly string _retryExchange = "retry_exchange";
//        private readonly string _completionExchange = "completion_exchange";
//        private readonly string _maintenanceQueue = "maintenance_queue";
//        private readonly string _retryQueue = "retry_queue";
//        private readonly string _completionQueue = "completion_queue";
//        private readonly string _maintenanceRoutingKey = "maintenance_request";
//        private readonly string _completionRoutingKey = "completion_request";
//        private readonly int _retryTtl = 180000; // 3 phút

//        public RabbitMQConsumer(
//            ILogger<RabbitMQConsumer> logger,
//            IServiceScopeFactory scopeFactory,
//            IConnectionFactory connectionFactory)
//        {
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

//            try
//            {
//                _connection = connectionFactory.CreateConnection();
//                _channel = _connection.CreateModel();
//                ConfigureRabbitMQ();
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
//                throw;
//            }
//        }

//        private void ConfigureRabbitMQ()
//        {
//            // Khai báo exchanges
//            _channel.ExchangeDeclare(_maintenanceExchange, ExchangeType.Direct, durable: true);
//            _channel.ExchangeDeclare(_retryExchange, ExchangeType.Direct, durable: true);
//            _channel.ExchangeDeclare(_completionExchange, ExchangeType.Direct, durable: true);
//            _logger.LogInformation("Exchanges declared: {MaintenanceExchange}, {RetryExchange}, {CompletionExchange}",
//                _maintenanceExchange, _retryExchange, _completionExchange);

//            // Cấu hình Dead-Letter Exchange cho maintenance_queue
//            var maintenanceQueueArgs = new Dictionary<string, object>
//            {
//                { "x-dead-letter-exchange", _retryExchange },
//                { "x-dead-letter-routing-key", _maintenanceRoutingKey }
//            };

//            // Khai báo maintenance_queue
//            _channel.QueueDeclare(_maintenanceQueue, durable: true, exclusive: false, autoDelete: false, arguments: maintenanceQueueArgs);
//            _channel.QueueBind(_maintenanceQueue, _maintenanceExchange, _maintenanceRoutingKey);
//            _logger.LogInformation("Queue {Queue} declared and bound to {Exchange} with routing key {RoutingKey}",
//                _maintenanceQueue, _maintenanceExchange, _maintenanceRoutingKey);

//            // Cấu hình TTL và Dead-Letter Exchange cho retry_queue
//            var retryQueueArgs = new Dictionary<string, object>
//            {
//                { "x-message-ttl", _retryTtl },
//                { "x-dead-letter-exchange", _maintenanceExchange },
//                { "x-dead-letter-routing-key", _maintenanceRoutingKey }
//            };

//            // Khai báo retry_queue
//            _channel.QueueDeclare(_retryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryQueueArgs);
//            _channel.QueueBind(_retryQueue, _retryExchange, _maintenanceRoutingKey);
//            _logger.LogInformation("Queue {Queue} declared with TTL {Ttl}ms and bound to {Exchange} with routing key {RoutingKey}",
//                _retryQueue, _retryTtl, _retryExchange, _maintenanceRoutingKey);

//            // Khai báo completion_queue
//            _channel.QueueDeclare(_completionQueue, durable: true, exclusive: false, autoDelete: false);
//            _channel.QueueBind(_completionQueue, _completionExchange, _completionRoutingKey);
//            _logger.LogInformation("Queue {Queue} declared and bound to {Exchange} with routing key {RoutingKey}",
//                _completionQueue, _completionExchange, _completionRoutingKey);

//            // Cấu hình QoS
//            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("RabbitMQConsumer is starting");

//            try
//            {
//                // Consumer cho maintenance_queue
//                var maintenanceConsumer = new AsyncEventingBasicConsumer(_channel);
//                maintenanceConsumer.Received += async (model, ea) =>
//                {
//                    using var scope = _scopeFactory.CreateScope();
//                    var consumerService = scope.ServiceProvider.GetRequiredService<IRabbitMQConsumerService>();

//                    try
//                    {
//                        var message = System.Text.Encoding.UTF8.GetString(ea.Body.ToArray());
//                        await consumerService.ProcessMessageAsync(_channel, ea, message);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogError(ex, "Error processing message from {Queue}", _maintenanceQueue);
//                    }
//                };

//                _channel.BasicConsume(queue: _maintenanceQueue, autoAck: false, consumer: maintenanceConsumer);
//                _logger.LogInformation("Consumer started for {Queue}", _maintenanceQueue);

//                // Consumer cho completion_queue
//                var completionConsumer = new AsyncEventingBasicConsumer(_channel);
//                completionConsumer.Received += async (model, ea) =>
//                {
//                    using var scope = _scopeFactory.CreateScope();
//                    var consumerService = scope.ServiceProvider.GetRequiredService<IRabbitMQConsumerService>();

//                    try
//                    {
//                        _logger.LogInformation("Received completion event, checking {Queue}", _retryQueue);
//                        await consumerService.MoveRequestsFromRetryToMaintenanceQueueAsync(_channel);
//                        _channel.BasicAck(ea.DeliveryTag, multiple: false);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogError(ex, "Error processing completion event from {Queue}", _completionQueue);
//                        _channel.BasicAck(ea.DeliveryTag, multiple: false); // Vẫn ack để tránh lặp vô hạn
//                    }
//                };

//                _channel.BasicConsume(queue: _completionQueue, autoAck: false, consumer: completionConsumer);
//                _logger.LogInformation("Consumer started for {Queue}", _completionQueue);

//                // Giữ service chạy
//                await Task.Delay(Timeout.Infinite, stoppingToken);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "RabbitMQConsumer encountered an error");
//            }
//        }

//        public override async Task StopAsync(CancellationToken cancellationToken)
//        {
//            _logger.LogInformation("RabbitMQConsumer is stopping");
//            _channel?.Close();
//            _connection?.Close();
//            await base.StopAsync(cancellationToken);
//        }

//        public override void Dispose()
//        {
//            _logger.LogInformation("Disposing RabbitMQConsumer");
//            _channel?.Dispose();
//            _connection?.Dispose();
//            base.Dispose();
//        }
//    }
//}