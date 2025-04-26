using EffiAP.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// EffiRent.Application/Commands/RoomCommand/CreateRoomCommand.cs
namespace EffiRent.Application.Commands.RoomCommand
{
    public class CreateRoomCommand : IRequest<Guid>
    {
        public Guid BranchID { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public Room.RoomStatus Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Notes { get; set; }
    }
}
