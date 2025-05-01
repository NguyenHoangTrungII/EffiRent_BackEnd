using System;

namespace EffiRent.Domain.Entities
{
    public class TenantRoom
    {
        // Primary key for TenantRoom record
        public Guid TenantRoomID { get; set; }

        // ID of the tenant (foreign key reference to tenant entity)
        public string TenantID { get; set; }

        // ID of the room being rented (foreign key reference to Room entity)
        public Guid RoomID { get; set; }

        // The start date of the rental period
        public DateTime StartDate { get; set; }

        // The end date of the rental period, nullable if the room is still rented
        public DateTime? EndDate { get; set; }

        // Navigation property referencing the associated room
        public Room? Room { get; set; }
    }
}
