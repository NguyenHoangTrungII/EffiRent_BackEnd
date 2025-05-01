using EffiAP.Domain.SeedWork;
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
    public interface IRabbitMQProducerService : ISingletonService
    {
        /// <summary>
        /// Gửi tin nhắn tới exchange với routing key, hoặc trực tiếp vào queue nếu exchange rỗng.
        /// </summary>
        /// <param name="message">Tin nhắn cần gửi.</param>
        /// <param name="exchange">Tên exchange (rỗng nếu gửi trực tiếp vào queue).</param>
        /// <param name="routingKey">Routing key hoặc tên queue.</param>
        Task PublishAsync<T>(T message, string exchange, string routingKey);
    }

   
}
