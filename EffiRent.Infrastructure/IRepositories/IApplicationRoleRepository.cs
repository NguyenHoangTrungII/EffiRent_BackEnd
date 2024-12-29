using EffiAP.Domain.SeedWork;
using System;
using System.Threading.Tasks;

namespace EffiAP.Infrastructure.IRepositories
{
    public interface IApplicationRoleRepository : IScopedService
    {
        Task<String?> GetTechnicianRoleIdAsync();
    }
}
