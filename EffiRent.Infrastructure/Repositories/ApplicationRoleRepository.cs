using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;
using EffiAP.Domain.Entities; // Assuming your ApplicationRole entity is here
using EffiAP.Infrastructure.IRepositories;

namespace EffiAP.Infrastructure.Repositories
{
    public class ApplicationRoleRepository : IApplicationRoleRepository
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public ApplicationRoleRepository(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<String?> GetTechnicianRoleIdAsync()
        {
            var technicianRole = await _roleManager.FindByNameAsync("Technician");
            return technicianRole?.Id;
        }
    }
}
