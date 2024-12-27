
using EffiAP.Application.Services.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Formats.Asn1.AsnWriter;

namespace EffiHR.Infrastructure.Middlewares
{
    public class SessionMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly IRedisService  _cacheService;



        public SessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var hasAuthorizeAttribute = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() != null;

            if (!hasAuthorizeAttribute)
            {
                await _next(context);
                return;
            }

            // Lấy sessionId từ cookie hoặc header (tùy thuộc vào cách bạn lưu trữ)
            var sessionId = context.Request.Cookies["SessionID"];

            if (!string.IsNullOrEmpty(sessionId))
            {
                // Tạo một scope mới
                using (var scope = context.RequestServices.CreateScope())
                {
                    // Lấy ICacheService từ scope
                    var cacheService = scope.ServiceProvider.GetRequiredService<IRedisService>();

                    // Kiểm tra session hợp lệ từ Redis
                    var session = await cacheService.GetSessionAsync(sessionId);

                    if (session != null)
                    {
                        // Tạo ClaimsIdentity từ thông tin session
                        var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, session.UserId),
                        new Claim(ClaimTypes.Name, session.UserName),
                        // Thêm roles vào claims nếu có
                        new Claim(ClaimTypes.Role, string.Join(",", session.Role))
                    };

                        var identity = new ClaimsIdentity(claims, "Session");
                        var principal = new ClaimsPrincipal(identity);

                        context.User = principal; // Thiết lập context.User
                    }
                    else
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Session is invalid or expired.");
                        return;
                    }
                }
            }
            else
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Session ID is missing.");
                return;
            }

            await _next(context);
        }


        
        private async Task<IDictionary<string, string>> GetSessionFromCache(string sessionId)
        {
            // Lấy session từ cache (Redis)
            var session = await _cacheService.GetHashAsync(sessionId);

            // Kiểm tra nếu session null và log thông tin để kiểm tra sau này
            if (session == null)
            {
                // Có thể thêm logging tại đây để theo dõi lỗi
                Console.WriteLine($"Session with ID {sessionId} not found in cache.");
            }

            return session;
        }

        private Task<bool> ValidateSessionAsync(string sessionId)
        {
            // Logic kiểm tra session từ Redis hoặc hệ thống lưu trữ phiên khác
            return Task.FromResult(true); // Giả sử session hợp lệ
        }
    }
}
