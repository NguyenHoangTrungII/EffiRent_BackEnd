using EffiAP.Application.Wrappers;
using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.MaintainRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Queries.IQueries
{
    public interface IMaintenanceQueryQueries : IScopedService
    {
        Task<ApiResponse<IEnumerable<MaintenanceRequestDTO>>> GetMaintenanceRequestsAsync();
    }
}
