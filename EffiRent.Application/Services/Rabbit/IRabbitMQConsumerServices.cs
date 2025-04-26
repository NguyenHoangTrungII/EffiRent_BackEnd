//using Microsoft.EntityFrameworkCore.Metadata;
using EffiAP.Domain.SeedWork;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EffiRent.Application.Services.Rabbit
{
    public interface IRabbitMQConsumerService: IScopedService
    {
        Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs eventArgs, string message);
        Task MoveRequestsFromRetryToMaintenanceQueueAsync(IModel channel);
    }
}