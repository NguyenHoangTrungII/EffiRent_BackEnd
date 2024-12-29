using EffiAP.Domain.Models;
using System;
using System.Collections.Generic;

namespace EffiAP.Domain.Entities
{
    public class MaintenanceCategory
    {
        public Guid Id { get; set; } // Unique identifier for the maintenance category

        public string Name { get; set; } // Name of the category (e.g., Electrical, Plumbing, Mechanical)
        public string Description { get; set; } // Detailed description of the category

        // Navigation property for associated maintenance requests
        public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } // 1-to-many relationship with MaintenanceRequest
    }
}
