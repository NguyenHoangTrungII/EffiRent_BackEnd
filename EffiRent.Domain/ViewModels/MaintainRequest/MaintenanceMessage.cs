using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.ViewModels.MaintainRequest
{
    public class MaintenanceMessage
    {
        public MaintenanceRequestCommandDTO request { get; set; }
        public List<string> FileBase64 { get; set; }
    }

    public class RabbitMaintenanceMessage
    {
        public MaintenanceRequestCommandDTO request { get; set; }
        public List<string> FileBase64 { get; set; }

        public Guid RequestId { get; set; }
    }
}
