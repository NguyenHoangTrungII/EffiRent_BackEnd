using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.ViewModels.MaintainRequest
{
    public class CompleteMaintenanceRequestDTO
    {
        public Guid RequestId { get; set; } // ID của yêu cầu bảo trì
        public string TechnicianId { get; set; } // ID của kỹ thuật viên
    }
}
