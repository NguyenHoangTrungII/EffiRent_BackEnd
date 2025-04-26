using EffiRent.Application.Services.Rabbit;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Rabbit
{
    public class RabbitMQProducerService : IRabbitMQProducerService
    {
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMQProducerService> _logger;

        public RabbitMQProducerService(IConnectionFactory connectionFactory, ILogger<RabbitMQProducerService> logger)
        {
            _connection = connectionFactory.CreateConnection();
            _logger = logger;
        }

        public async Task PublishAsync<T>(T message, string exchange, string routingKey)
        {
            using var channel = _connection.CreateModel();
            channel.ExchangeDeclare(exchange, ExchangeType.Direct, durable: true);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            channel.BasicPublish(exchange, routingKey, properties, body);
            _logger.LogInformation("Published message to exchange {Exchange} with routing key {RoutingKey}", exchange, routingKey);
        }
    }
}
