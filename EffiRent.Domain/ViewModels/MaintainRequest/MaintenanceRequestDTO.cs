//using EffiAP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.ViewModels.MaintainRequest
{
    public class MaintenanceRequestDTO
    {

        public Guid RoomId { get; set; }
        public string Description { get; set; }
        public string? TechnicianId { get; set; }
        public string CustomerId { get; set; }
        public Guid CategoryId { get; set; }
        public int PriorityLevel { get; set; }
        public string Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public string CustomerFeedback { get; set; }


    }

    public class MaintenanceRequestCommandDTO
    {
        public Guid requestId { get; set; }
        public Guid RoomId { get; set; }
        public string Description { get; set; }
        public string? TechnicianId { get; set; }
        public string CustomerId { get; set; }
        public Guid CategoryId { get; set; }
        public int PriorityLevel { get; set; }
        public string Status { get; set; }

        public DateTime? CreatedAt { get; set; }
        public string CustomerFeedback { get; set; }


    }
}
