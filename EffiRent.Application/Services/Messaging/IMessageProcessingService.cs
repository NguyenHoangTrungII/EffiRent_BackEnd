using EffiAP.Domain.SeedWork;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Messaging
{
    public interface IMessageProcessingService : ISingletonService
    {
        Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs ea, string message);
    }
}
