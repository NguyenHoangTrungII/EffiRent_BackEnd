using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EffiAP.Domain.ViewModels.Users;

namespace EffiAP.Application.Services.Redis
{
    public class RedisService : IRedisService
    {
        private readonly IDistributedCache _cache;

        public RedisService(IDistributedCache distributedCache)
        {
            _cache = distributedCache;
        }

        // Lưu đối tượng vào Redis với thời gian hết hạn
        public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
        {
            // Thiết lập tùy chọn cho cache
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            // Serialize đối tượng thành chuỗi JSON
            var serializedValue = JsonSerializer.Serialize(value);

            // Lưu giá trị vào Redis
            await _cache.SetStringAsync(key, serializedValue, options);
        }

        // Lấy đối tượng từ Redis
        public async Task<T> GetAsync<T>(string key)
        {
            // Lấy chuỗi JSON từ Redis
            var serializedValue = await _cache.GetStringAsync(key);

            if (serializedValue == null)
            {
                return default(T); // Không tìm thấy dữ liệu trong cache
            }

            // Deserialize chuỗi JSON về đối tượng
            return JsonSerializer.Deserialize<T>(serializedValue);
        }

        // Xóa đối tượng khỏi Redis
        public async Task RemoveAsync(string key)
        {
            // Xóa dữ liệu khỏi Redis
            await _cache.RemoveAsync(key);
        }

        // Lưu session của người dùng và thêm sessionId vào danh sách sessionIds của user
        public async Task StoreSessionAsync(UserSession session)
        {
            // Lưu session cụ thể vào cache với sessionId là khóa
            var expiration = session.ExpiresAt - DateTime.UtcNow;
            if (expiration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Session has already expired.");
            }

            // Lưu session vào Redis với khóa sessionId
            await SetAsync<UserSession>($"session:{session.SessionId}", session, expiration);

            // Lấy danh sách sessionId của người dùng (nếu đã tồn tại)
            var userSessions = await GetAsync<HashSet<string>>($"user-sessions:{session.UserId}") ?? new HashSet<string>();

            // Thêm sessionId mới vào danh sách
            userSessions.Add(session.SessionId);

            // Lưu lại danh sách sessionIds vào cache
            await SetAsync($"user-sessions:{session.UserId}", userSessions, expiration);
        }

        public async Task<UserSession> GetSessionAsync(string sessionId)
        {
            // Lấy session từ Redis bằng sessionId
            var session = await GetAsync<UserSession>(sessionId);

            // Kiểm tra nếu session không tồn tại hoặc đã hết hạn
            if (session == null || session.ExpiresAt <= DateTime.UtcNow)
            {
                // Nếu không tìm thấy session hoặc session đã hết hạn, trả về null hoặc xử lý theo yêu cầu của bạn
                return null;
            }

            return session;
        }


        // Lấy tất cả các session của người dùng
        public async Task<List<UserSession>> GetAllSessionsForUserAsync(string userId)
        {
            // Lấy danh sách sessionId của người dùng từ Redis
            var sessionIds = await GetAsync<HashSet<string>>($"user-sessions:{userId}");
            if (sessionIds == null)
            {
                return new List<UserSession>(); // Không có phiên nào cho user này
            }

            var sessions = new List<UserSession>();

            // Lặp qua danh sách sessionId và lấy thông tin từng session
            foreach (var sessionId in sessionIds)
            {
                var session = await GetAsync<UserSession>($"session:{sessionId}");
                if (session != null)
                {
                    sessions.Add(session);
                }
            }

            return sessions;
        }

        // Xóa một sessionId cụ thể
        public async Task RemoveSessionAsync(string userId, string sessionId)
        {
            // Xóa session khỏi Redis
            await RemoveAsync($"session:{sessionId}");

            // Lấy danh sách sessionIds của người dùng
            var userSessions = await GetAsync<HashSet<string>>($"user-sessions:{userId}");
            if (userSessions != null)
            {
                // Xóa sessionId khỏi danh sách
                userSessions.Remove(sessionId);

                // Cập nhật lại danh sách sessionIds
                await SetAsync($"user-sessions:{userId}", userSessions, TimeSpan.FromHours(1));
            }
        }

        // Lưu dữ liệu vào Redis Hash
        public async Task SetHashAsync(string key, IDictionary<string, string> values, TimeSpan expiration)
        {
            // Lưu giá trị vào Redis Hash
            foreach (var kvp in values)
            {
                await _cache.SetStringAsync($"{key}:{kvp.Key}", kvp.Value, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                });
            }

            // Thiết lập thời gian hết hạn cho hash
            await _cache.SetStringAsync(key, "hash_exists", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            });
        }

        // Lấy dữ liệu từ Redis Hash
        public async Task<IDictionary<string, string>> GetHashAsync(string key)
        {
            var result = new Dictionary<string, string>();

            // Lấy tất cả các key trong hash
            var keys = await _cache.GetStringAsync(key);
            if (keys != null)
            {
                foreach (var keyValue in keys.Split(','))
                {
                    var value = await _cache.GetStringAsync($"{key}:{keyValue}");
                    if (value != null)
                    {
                        result[keyValue] = value;
                    }
                }
            }

            return result;
        }
    }
}
