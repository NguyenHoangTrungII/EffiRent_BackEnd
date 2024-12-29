using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.ViewModels.MaintainRequest
{
    public class ConfirmMaintenanceCompletionByCustomerDTO
    {
        public Guid MaintenanceRequestId { get; set; }
        public string? CustomerFeedback { get; set; }
       
    }
}
