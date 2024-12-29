using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.MaintainRequest;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Commands.MaintainRequestCommand
{
    public class ConfirmMaintenanceCompletionByCustomerCommand : IRequest<ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>>
    {
        public Guid MaintenanceRequestId { get; set; } 
        public string? CustomerFeedback { get; set; } 
        public ConfirmMaintenanceCompletionByCustomerCommand(Guid maintenancerequestId, string customerfeedback)
        {
            MaintenanceRequestId = maintenancerequestId;
            CustomerFeedback = customerfeedback;
        }
    }

}
