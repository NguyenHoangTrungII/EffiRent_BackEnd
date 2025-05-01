using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//using EffiAP.Domain.Entities; // Đảm bảo bạn có namespace này
using EffiAP.Infrastructure.IRepositories;

namespace EffiAP.Infrastructure.Repositories
{
    public class ApplicationUserRoleRepository : IApplicationUserRoleRepository
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ApplicationUserRoleRepository(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<string>> GetTechniciansAsync(string technicianRoleId)
        {
            // Lấy danh sách người dùng trong vai trò "Technician"
            var usersInRole = await _userManager.GetUsersInRoleAsync("Technician");

            // Lọc ra những người không bị khóa và trả về danh sách ID
            var technicianIds = usersInRole
                //.Where(u => !u.LockoutEnabled)
                .Select(u => u.Id)
                .ToList();

            return technicianIds;
        }

    }
}
