using EffiAP.Domain.SeedWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Redis
{
    public interface  ICacheRedis : IScopedService
    {
        Task<string> SetPopAsync(string key); // Lấy và xóa một phần tử từ Set
        Task SetAddAsync(string key, string value); // Thêm một phần tử vào Set
        Task SetRemoveAsync(string key, string value); // Xóa một phần tử khỏi Set
        Task<IEnumerable<string>> SetMembersAsync(string key); // Lấy tất cả phần tử trong Set
        Task KeyDeleteAsync(string key); // Xóa key
    }
}
