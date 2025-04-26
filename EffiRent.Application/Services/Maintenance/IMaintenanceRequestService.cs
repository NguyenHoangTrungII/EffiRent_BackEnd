using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.MaintainRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Maintenance
{
    public interface IMaintenanceRequestService : IScopedService
    {
        Task<MaintenanceRequestCommandDTO> ProcessMaintenanceRequestAsync(MaintenanceRequestCommandDTO request, List<string> fileUrls);
    }
}
