using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiHR.Application.Interfaces
{
    public interface ISessionService : IScopedService
    {
        Task StoreSessionAsync(UserSession session);
        Task<List<UserSession>> GetSessionsByUserAsync(string userId);
        Task InvalidateSessionAsync(string sessionId);
        Task InvalidateAllSessionsExceptCurrentAsync(string userId, string currentSessionId);
    }
}
