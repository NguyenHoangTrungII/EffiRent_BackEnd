using EffiAP.Application.Services.Redis;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Redis
{
    public class CacheRedis : ICacheRedis
    {
        private readonly IDatabase _database;

        public CacheRedis(IConnectionMultiplexer redis)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis), "Redis connection multiplexer cannot be null.");
            }
            try
            {
                _database = redis.GetDatabase();
                if (_database == null)
                {
                    throw new InvalidOperationException("Failed to initialize Redis database.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to connect to Redis database.", ex);
            }
        }

        public async Task<string> SetPopAsync(string key)
        {
            var value = await _database.SetPopAsync(key);
            return value.HasValue ? value.ToString() : null;
        }

        public async Task SetAddAsync(string key, string value)
        {
            await _database.SetAddAsync(key, value);
        }

        public async Task SetRemoveAsync(string key, string value)
        {
            await _database.SetRemoveAsync(key, value);
        }

        public async Task<IEnumerable<string>> SetMembersAsync(string key)
        {
            if (_database == null)
            {
                throw new InvalidOperationException("Redis database is not initialized.");
            }
            var members = await _database.SetMembersAsync(key);
            return members.Select(m => m.ToString());
        }

        public async Task KeyDeleteAsync(string key)
        {
            await _database.KeyDeleteAsync(key);
        }
    }
}
