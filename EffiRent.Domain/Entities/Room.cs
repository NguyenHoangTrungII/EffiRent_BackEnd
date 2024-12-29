using EffiAP.Domain.Models;
using System;
using System.Collections.Generic;

namespace EffiAP.Domain.Entities
{
    public class Room
    {
        // Unique identifier for the room
        public Guid Id { get; set; }

        // Name of the room (e.g., Room 101, Office 3A)
        public string Name { get; set; }

        // Location of the room (e.g., Building A, Floor 2)
        public string Location { get; set; }

        // Foreign key linking to the branch the room belongs to
        public Guid BranchID { get; set; }

        // Status of the room, represented as an enum
        public RoomStatus Status { get; set; }

        // Date when the room became available or occupied
        public DateTime? StartDate { get; set; }

        // Date when the room will be vacated or released
        public DateTime? EndDate { get; set; }

        // Additional notes or remarks about the room
        public string Notes { get; set; }

        // Navigation property for the room's related maintenance requests (1-n relationship)
        public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; }

        // Navigation property to the Branch entity
        public Branch Branch { get; set; }

        // Enum to represent the different statuses a room can have
        public enum RoomStatus
        {
            Available,     // Room is available for use
            Occupied,      // Room is currently occupied
            Cleaning,      // Room is being cleaned
            Maintenance,   // Room is under maintenance
            Processing,    // Room is being prepared for use
            Locked         // Room is locked and inaccessible
        }
    }
}
