using EffiAP.Application.Auth;
using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Application.Queries.IQueries;
using EffiRent.Domain.Entities;
using EffiAP.Domain.ViewModels.Branch;
using EffiAP.Domain.ViewModels.Users;
using EffiAP.Presentation.Abstractions;
using EffiHR.Application.Interfaces;
using EffiRent.Domain.ViewModels.Users;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticateController : ApiController
    {
        private readonly ISender _sender;
        private readonly IAuthentication _identityServices;
        private readonly ISessionService _sessionService;

        public AuthenticateController(ISender sender, IAuthentication identityServices, ISessionService sessionService ) : base(sender)
        {
            _sender = sender;
            _identityServices = identityServices;
            _sessionService = sessionService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginForm login)
        {
            var result = await _identityServices.Authenticate(login);

            if (!result.Succeeded)
                return BadRequest(result);

            UserSession userSession  = result.Data;

            userSession.DeviceInfo = Request.Headers["User-Agent"].ToString();
            userSession.IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Lưu phiên đăng nhập vào Redis
            await _sessionService.StoreSessionAsync(userSession);

            // Thiết lập cookie cho session ID
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30), // Thời gian hết hạn cookie
                SameSite = SameSiteMode.Strict, // Có thể điều chỉnh theo nhu cầu
                Secure = true // Chỉ sử dụng HTTPS
            };

            HttpContext.Response.Cookies.Append("SessionID", userSession.SessionId, cookieOptions);


            return Ok (result);

        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterForm registerForm)
        {
            // Validate input (you can add more validation here if needed)
            if (registerForm == null)
                return BadRequest("Invalid registration data.");

            // Call the registration service to create the user
            var result = await _identityServices.Register(registerForm);

            if (!result.Succeeded)
                return BadRequest(result);

            // If registration succeeds, authenticate the user (optional)
            var loginResult = await _identityServices.Authenticate(new LoginForm
            {
                UserName = registerForm.Username,
                Password = registerForm.Password
            });

            if (!loginResult.Succeeded)
                return BadRequest("Authentication failed after registration.");

            UserSession userSession = loginResult.Data;

            userSession.DeviceInfo = Request.Headers["User-Agent"].ToString();
            userSession.IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Store the session in Redis
            await _sessionService.StoreSessionAsync(userSession);

            // Set session ID in cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30),
                SameSite = SameSiteMode.Strict,
                Secure = true
            };

            HttpContext.Response.Cookies.Append("SessionID", userSession.SessionId, cookieOptions);

            return Ok(new { Message = "Registration successful", User = userSession });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
        {
            var result = await _identityServices.ForgetPassword(new ForgotPasswordRequest
            {
                Email = model.Email,
            } );

            if (!result.Succeeded)
            {
                return  BadRequest(result.Errors);
            }

            return Ok(new { Message = result.Message });
        }

    }
}
