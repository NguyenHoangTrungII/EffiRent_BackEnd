using EffiAP.Domain.ViewModels.Users;
using EffiAP.Domain.SeedWork;
using EffiAP.Infrastructure.EntityModels;
using EffiAP.Application.Wrappers;
using EffiRent.Domain.ViewModels.Users;
using Microsoft.AspNetCore.Identity;
using EffiHR.Infrastructure.Data;

namespace EffiAP.Application.Auth
{
    public interface IAuthentication : IScopedService
    {
        Task<ApiResponse<UserSession>> Authenticate(LoginForm loginForm);
        Task<ApiResponse<ApplicationUser>> Register(RegisterForm  registerForm);
        Task<ApiResponse<bool>> ForgetPassword(ForgotPasswordRequest forgotPasswordRequest);

        //bool ValidatePassword(string password);
        //Task<string> GenergateToken(string userName, string branchID);

    }
}
