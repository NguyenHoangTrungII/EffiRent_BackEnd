using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.MaintainRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Technician
{
    public interface ITechnicianAssignmentService : IScopedService
    {
        Task<string> AssignTechnicianAsync(MaintenanceRequestCommandDTO request);
    }
}
