using EffiAP.Domain.SeedWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.IRepositories
{
    public interface IApplicationUserRoleRepository : IScopedService
    {
        Task<List<string>> GetTechniciansAsync(string technicianRoleId);

    }
}
