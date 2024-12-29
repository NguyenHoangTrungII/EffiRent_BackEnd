using EffiAP.Application.Services.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace EffiAP.Application.Services.Messaging
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly ILogger<RabbitMQConsumer> _logger;
        //private readonly IRabbitMQConsumerService _rabbitMQConsumerService;
        private readonly IServiceScopeFactory _scopeFactory; // Inject IServiceScopeFactory


        public RabbitMQConsumer( ILogger<RabbitMQConsumer> logger, IServiceScopeFactory scopeFactory)
        {
            //_rabbitMQConsumerService = rabbitMQConsumerService;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RabbitMQConsumer is starting.");

            try
            {
                var factory = new ConnectionFactory() { HostName = "localhost" };
                _logger.LogInformation("Connecting to RabbitMQ...");

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                _logger.LogInformation("RabbitMQ connection established.");

                // Declare exchanges and queues
                channel.ExchangeDeclare(exchange: "maintenance_exchange", type: ExchangeType.Direct);
                channel.ExchangeDeclare(exchange: "retry_exchange", type: ExchangeType.Direct);

                // Định nghĩa Dead Letter Exchange
                var deadLetterExchange = "retry_exchange"; // Tên của DLX

                var maintenanceQueueArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", deadLetterExchange }, // Chỉ định DLX cho hàng đợi
                    { "x-dead-letter-routing-key", "maintenance_request" } // Routing key for DLX

                };

                // Declare main queue with arguments
                channel.QueueDeclare("maintenance_queue", durable: true, exclusive: false, autoDelete: false, arguments: maintenanceQueueArgs);
                _logger.LogInformation("Queue 'maintenance_queue' declared with DLX.");


                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received message: {Message}", "in maintaenance");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var rabbitMQConsumerService = scope.ServiceProvider.GetRequiredService<IRabbitMQConsumerService>();

                        try
                        {
                            // Xử lý tin nhắn
                            await rabbitMQConsumerService.ProcessMessageAsync(channel, ea, message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Error processing message: {Error}", ex.Message);
}
                    }

                };

                _logger.LogInformation("Registering consumer to listen for messages...");
                channel.BasicConsume(queue: "maintenance_queue", autoAck: false, consumer: consumer);


                // Declare retry queue with TTL
                var ttl = 180000; // TTL in milliseconds
                var arguments = new Dictionary<string, object>
                {
                    { "x-message-ttl", ttl }, // Set TTL for messages
                    { "x-dead-letter-exchange", "maintenance_exchange" }, // Set dead-letter exchange
                    { "x-dead-letter-routing-key", "maintenance_request" } // Routing key for DLX
                };
                channel.QueueDeclare(queue: "retry_queue", durable: true, exclusive: false, autoDelete: false, arguments: arguments);
                _logger.LogInformation("Queue 'retry_queue' declared with TTL.");

                
                // Bind queues to exchanges
                channel.QueueBind(queue: "maintenance_queue", exchange: "maintenance_exchange", routingKey: "maintenance_request");
                _logger.LogInformation("Queue bound to exchange 'maintenance_exchange'.");

                

                channel.QueueBind(queue: "retry_queue", exchange: "retry_exchange", routingKey: "maintenance_request");
                _logger.LogInformation("Queue bound to exchange 'retry_exchange'.");

           
               
                _logger.LogInformation("Consumer is listening for messages.");

                // Consumer for CompletionQueue (lắng nghe sự kiện hoàn thành)
                // Declare completion queue


                //channel.QueueDeclare(queue: "completion_queue", durable: true, exclusive: false, autoDelete: false);
                //_logger.LogInformation("Queue 'completion_queue' declared.");

                //channel.QueueBind(queue: "completion_queue", exchange: "completion_exchange", routingKey: "completion_request");


                //var completionConsumer = new EventingBasicConsumer(channel);
                //completionConsumer.Received += async (model, ea) =>
                //{
                //    var body = ea.Body.ToArray();
                //    var message = Encoding.UTF8.GetString(body);

                //    _logger.LogInformation("Technician available, checking retry_queue...");

                //    using (var scope = _scopeFactory.CreateScope())
                //    {
                //        var rabbitMQConsumerService = scope.ServiceProvider.GetRequiredService<IRabbitMQConsumerService>();
                //        await rabbitMQConsumerService.MoveRequestsFromRetryToMaintenanceQueueAsync(channel);
                //    }
                //};

                //channel.BasicConsume(queue: "completion_queue", autoAck: true, consumer: completionConsumer)


                //_logger.LogInformation("Consumer is listening for messages on 'completion_queue'.");

                // Khai báo completion queue
                channel.ExchangeDeclare(exchange: "completion_exchange", type: ExchangeType.Direct, durable: true);
                channel.QueueDeclare(queue: "completion_queue", durable: true, exclusive: false, autoDelete: false);
                _logger.LogInformation("Queue 'completion_queue' declared.");

                // Liên kết completion queue với completion exchange
                channel.QueueBind(queue: "completion_queue", exchange: "completion_exchange", routingKey: "completion_request");
                _logger.LogInformation("Queue 'completion_queue' bound to exchange 'completion_exchange'.");

                var completionConsumer = new EventingBasicConsumer(channel);
                completionConsumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Technician available, checking retry_queue...");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var rabbitMQConsumerService = scope.ServiceProvider.GetRequiredService<IRabbitMQConsumerService>();
                        await rabbitMQConsumerService.MoveRequestsFromRetryToMaintenanceQueueAsync(channel);
                    }
                };

                // Bắt đầu lắng nghe completion queue
                channel.BasicConsume(queue: "completion_queue", autoAck: true, consumer: completionConsumer);
                _logger.LogInformation("Consumer is listening for messages on 'completion_queue'.");


                // Keep the background service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"RabbitMQConsumer encountered an error: {ex.Message}");
            }
        }


    }
}
