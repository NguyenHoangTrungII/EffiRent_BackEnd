using EffiAP.Domain.Models;
using System;

namespace EffiAP.Domain.Entities
{
    public class CustomerFeedback
    {
        public Guid Id { get; set; } // Unique identifier for the feedback

        public Guid MaintenanceRequestId { get; set; } // Foreign key to the associated maintenance request

        public string Feedback { get; set; } // Feedback content provided by the customer

        public DateTime CreatedAt { get; set; } // Timestamp indicating when the feedback was created

        // Navigation property for the associated maintenance request
        public virtual MaintenanceRequest MaintenanceRequest { get; set; } // Many-to-one relationship with MaintenanceRequest
    }
}
