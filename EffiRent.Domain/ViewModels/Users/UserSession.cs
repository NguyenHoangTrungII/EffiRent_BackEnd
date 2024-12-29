using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Domain.ViewModels.Users
{
    public class UserSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        //public List<string>? Roles { get; set; }
        public string Role { get; set; } // Chỉ lưu 1 quyền
        public string? TenantId { get; set; }
        public string JwtToken { get; set; }
        public string DeviceInfo { get; set; }
        public string IPAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }
}
