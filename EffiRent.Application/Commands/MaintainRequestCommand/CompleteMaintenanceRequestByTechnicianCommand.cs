using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Commands.MaintainRequestCommand
{
    public class CompleteMaintenanceRequestByTechnicianCommand : IRequest<bool>
    {
        public Guid RequestId { get; set; }
        public string TechnicianId { get; set; }

        public CompleteMaintenanceRequestByTechnicianCommand(Guid requestId, string technicianId)
        {
            RequestId = requestId;
            TechnicianId = technicianId;
        }
    }

}
