using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.MaintainRequest;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Messaging
{
    public interface IRabbitMQProducerService : ISingletonService
    {
        Task QueueMaintenanceRequestAsync(MaintenanceRequestCommandDTO request);
        Task SendToQueueWithTTLAsync(string queuename, MaintenanceRequestCommandDTO request);
        Task SendToMaintenanceQueueAsync(MaintenanceRequestCommandDTO message, List<IFormFile> file);

        //Task SendToRetryQueueAsync(MaintenanceRequestCommandDTO message, List<IFormFile> file);

        Task SendToCompletionQueueAsync(string requestDto);
    }
}
