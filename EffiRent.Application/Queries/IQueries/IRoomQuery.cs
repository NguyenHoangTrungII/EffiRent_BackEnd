using EffiAP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Queries.IQueries
{
    public interface IRoomQuery
    {
        Task<List<Room>> GetRoomsByBranchAsync(Guid branchId);
    }
}
