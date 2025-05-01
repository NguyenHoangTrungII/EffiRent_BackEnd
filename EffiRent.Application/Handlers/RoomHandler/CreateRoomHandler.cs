// EffiRent.Application/Handlers/RoomHandler/RoomHandler.cs
using EffiAP.Application.Queries;
using EffiRent.Domain.Entities;
//using EffiAP.Domain.Models;
using EffiAP.Infrastructure.IRepositories;
using EffiRent.Application.Commands.RoomCommand;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace EffiRent.Application.Handlers.RoomHandler
{
    public class CreateRoomHandler : BaseQuery, IRequestHandler<CreateRoomCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateRoomHandler(IUnitOfWork unitOfWork, IConfiguration configuration) : base(configuration)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
        {
            // Kiểm tra xem Branch có tồn tại không
            var branch = await _unitOfWork.Repository.GetByIdAsync<Room>(request.BranchID);
            if (branch == null)
            {
                throw new Exception("Branch not found.");
            }

            var room = new Room
            {
                Id = Guid.NewGuid(),
                BranchID = request.BranchID,
                Name = request.Name,
                Location = request.Location,
                Status = request.Status,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Notes = request.Notes,
                MaintenanceRequests = new List<MaintenanceRequest>()
            };

            await  _unitOfWork.Repository.AddAsync(room);
            await _unitOfWork.SaveChangesAsync();

            return room.Id;
        }
    }
}