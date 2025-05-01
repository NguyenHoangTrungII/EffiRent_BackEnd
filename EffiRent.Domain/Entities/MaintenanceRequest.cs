using System;
using System.Collections.Generic;
//using EffiAP.Domain.Entities;

namespace EffiRent.Domain.Entities
{
    public class MaintenanceRequest
    {
        public Guid Id { get; set; } // Unique identifier for the maintenance request

        public string CustomerId { get; set; } // The ID of the customer making the request
        public string? TechnicianId { get; set; } // The ID of the technician assigned to the request

        public string Status { get; set; } // Current status of the request (e.g., Pending, Completed, Reopened)
        public int PriorityLevel { get; set; } // Priority level of the request (1 being the highest)

        public DateTime CreatedAt { get; set; } // Timestamp when the request was created
        public DateTime UpdatedAt { get; set; } // Timestamp for when the request was last updated
        public DateTime? CompletedAt { get; set; } // Timestamp for when the task was completed, nullable in case it's pending

        public bool IsCustomerConfirmed { get; set; } // Indicates if the customer has confirmed the completion of the request
        public bool TechnicianConfirmed { get; set; } // Indicates if the technician has confirmed the completion of the task

        //public string? CustomerFeedback { get; set; } // Feedback provided by the customer regarding the completed work

        // Foreign key for linking to the maintenance category
        public Guid CategoryId { get; set; } // The ID of the maintenance category the request belongs to

        // Navigation property for the associated maintenance category
        public virtual MaintenanceCategory Category { get; set; } // 1-to-1 relationship with MaintenanceCategory

        // Navigation property for customer feedback, allowing for multiple feedback entries per request
        public virtual ICollection<CustomerFeedback> Feedbacks { get; set; } // 1-to-many relationship with CustomerFeedback
        public Guid RoomId { get; set; } // ID của phòng liên quan đến yêu cầu bảo trì
        public virtual Room Room { get; set; } // Điều hướng tới Room (quan hệ 1-n với Room)
        public virtual ICollection<MaintenanceRequestImage> Images { get; set; } // 1-to-many relationship with MaintenanceRequestImage



    }
}
