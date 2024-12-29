using EffiAP.Domain.SeedWork;
using EffiAP.Domain.ViewModels.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Services.Redis
{
    public interface IRedisService : IScopedService
    {
        Task SetAsync<T>(string key, T value, TimeSpan expiration);
        Task<T> GetAsync<T>(string key);
        Task<UserSession> GetSessionAsync(string sessionId);

        Task RemoveAsync(string key);
        Task<IDictionary<string, string>> GetHashAsync(string key);

    }
}
