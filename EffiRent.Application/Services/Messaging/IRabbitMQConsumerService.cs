using EffiAP.Domain.SeedWork;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Messaging
{
    public interface IRabbitMQConsumerService : ISingletonService
    {
        Task MoveRequestsFromRetryToMaintenanceQueueAsync(IModel channel);
        Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs ea, string message);
    }
}
