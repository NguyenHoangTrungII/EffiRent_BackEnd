// EffiRent.Application/Queries/IQueries/RoomQuery.cs
using EffiAP.Application.Queries;
using EffiRent.Domain.Entities;
using EffiAP.Infrastructure.IRepositories;
using Microsoft.Extensions.Configuration;

namespace EffiRent.Application.Queries.IQueries
{
    public class RoomQuery : BaseQuery, IRoomQuery
    {
        private readonly IUnitOfWork _unitOfWork;

        public RoomQuery(IConfiguration configuration, IUnitOfWork unitOfWork)
            : base(configuration)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<Room>> GetRoomsByBranchAsync(Guid branchId)
        {
            var rooms =  _unitOfWork.Repository.Get<Room>(r => r.BranchID == branchId);
            return rooms.ToList();
        }
    }
}