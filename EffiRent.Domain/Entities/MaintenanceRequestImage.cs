//using EffiAP.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Domain.Entities
{
    public class MaintenanceRequestImage
    {
        public Guid Id { get; set; } // Unique identifier for the image

        public Guid MaintenanceRequestId { get; set; } // Foreign key for the associated maintenance request
        public string ImageUrl { get; set; } // URL or path to the image
        public DateTime CreatedAt { get; set; } // Timestamp for when the image was added

        // Navigation property for the associated maintenance request
        public virtual MaintenanceRequest MaintenanceRequest { get; set; }
    }
}
