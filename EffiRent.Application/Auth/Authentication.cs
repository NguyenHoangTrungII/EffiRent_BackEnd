using Azure.Core;
//using Dapper;
using EffiAP.Application.Queries;
using EffiAP.Application.Queries.IQueries;
//using EffiAP.Application.Services.ErrorHandler;
//using EffiAP.Application.Services.IServices;
using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.Users;
using EffiAP.Infrastructure.EntityModels;
using EffiAP.Infrastructure.IRepositories;
using EffiHR.Infrastructure.Data;
using EffiRent.Application.Services.Email;
using EffiRent.Domain.ViewModels.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EffiAP.Application.Auth
{
    public class Authentication : BaseQuery, IAuthentication
    {
        private EffiRentContext _context;
        private readonly IEncryption _encryption;
        public IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        //public IUserQuery _userQuery;
        //public readonly IIdentityService _identityService;
        public readonly IUnitOfWork _unitOfWork;

        public Authentication(EffiRentContext context, IEncryption encryption, IConfiguration configuration, IUnitOfWork unitOfWork, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IEmailService emailService) : base(configuration)
        {
            _context = context;
            _encryption = encryption;
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
        }

        public async Task<string> GenergateToken(string userName, string branchID)
        {
            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, _configuration["Jwt:Subject"]),
                        new Claim("UserName", _encryption.Encrypt(userName.ToString())),
                        new Claim("BranchID", _encryption.Encrypt(branchID.ToString())),
                    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddHours(12),
                signingCredentials: signIn);


            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        //From Old service
        public async Task<ApiResponse<UserSession>> Authenticate(LoginForm loginForm)
        {
            var user = await _userManager.FindByNameAsync(loginForm.UserName);
            if (user == null)
            {
                return new ApiResponse<UserSession>("Invalid username or password.");
            }

            var result = await _userManager.CheckPasswordAsync(user, loginForm.Password);
            if (!result)
            {
                return new ApiResponse<UserSession>("Invalid username or password.");
            }

            var userRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            var userSession = new UserSession
            {
                SessionId = Guid.NewGuid().ToString(),
                UserId = user.Id,
                UserName = user.UserName,
                Role = userRole,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(3),
                IsActive = true
            };

            return new ApiResponse<UserSession>(userSession, "Authentation Sucess");



            // Lưu phiên đăng nhập vào Redis
            //await _sessionService.StoreSessionAsync(userSession);

            // Thiết lập cookie cho session ID
            //var cookieOptions = new CookieOptions
            //{
            //    HttpOnly = true,
            //    Expires = DateTimeOffset.UtcNow.AddMinutes(30), // Thời gian hết hạn cookie
            //    SameSite = SameSiteMode.Strict, // Có thể điều chỉnh theo nhu cầu
            //    Secure = true // Chỉ sử dụng HTTPS
            //};

            // Lưu sessionId vào cookie để duy trì đăng nhập
            //HttpContext.Response.Cookies.Append("SessionID", userSession.SessionId, cookieOptions);


            //try
            //{
            //    // Tìm kiếm người dùng theo username
            //    var user = await _userManager.FindByNameAsync(loginRequest.Username);
            //    if (user == null)
            //    {
            //        return Unauthorized(new { message = "Invalid username or password." });
            //    }

            //    // Kiểm tra mật khẩu
            //    var result = await _userManager.CheckPasswordAsync(user, loginRequest.Password);
            //    if (!result)
            //    {
            //        return Unauthorized(new { message = "Invalid username or password." });
            //    }

            //    // Lấy các quyền (roles) của người dùng
            //    var userRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            //    // Tạo thông tin phiên (session) cho người dùng
            //    var userSession = new UserSession
            //    {
            //        SessionId = Guid.NewGuid().ToString(), // Tạo SessionId ngẫu nhiên
            //        UserId = user.Id,
            //        UserName = user.UserName,
            //        Role = userRole, // Lưu roles vào session
            //        TenantId = tenantId,   // Thêm TenantId vào session
            //        DeviceInfo = Request.Headers["User-Agent"].ToString(), // Thông tin thiết bị từ User-Agent
            //        IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), // Địa chỉ IP của người dùng
            //        CreatedAt = DateTime.UtcNow,
            //        ExpiresAt = DateTime.UtcNow.AddHours(3),  // Thiết lập thời gian hết hạn
            //        IsActive = true  // Đánh dấu phiên đang hoạt động
            //    };

            //    // Lưu phiên đăng nhập vào Redis
            //    await _sessionService.StoreSessionAsync(userSession);

            //    // Thiết lập cookie cho session ID
            //    var cookieOptions = new CookieOptions
            //    {
            //        HttpOnly = true,
            //        Expires = DateTimeOffset.UtcNow.AddMinutes(30), // Thời gian hết hạn cookie
            //        SameSite = SameSiteMode.Strict, // Có thể điều chỉnh theo nhu cầu
            //        Secure = true // Chỉ sử dụng HTTPS
            //    };

            //    // Lưu sessionId vào cookie để duy trì đăng nhập
            //    HttpContext.Response.Cookies.Append("SessionID", userSession.SessionId, cookieOptions);


            //    if (password != user.PasswordHash)
            //    {
            //        var u = await _unitOfWork.Repository.GetAsync<User>(x => x.UserName == user.UserName);
            //        u.FailedLoginCount = u.FailedLoginCount + 1;

            //        /* //message = new ErrorResponse(0001,null);*/
            //        if (u.FailedLoginCount > 10)
            //        {
            //            u.Blocked = true;
            //            await _unitOfWork.SaveEntitiesAsync();
            //            return new ApiResponse<ApplicationUser>(null, _identityService.GetUserLangId() == "vi" ? "Tài khoản đã bị khóa. Liên hệ admin để mở khóa!" : "Your account has been blocked. Please contact admin to unlock!");
            //        }
            //        await _unitOfWork.SaveEntitiesAsync();
            //        return new ApiResponse<ApplicationUser>( null, _identityService.GetUserLangId() == "vi" ? "Tên đăng nhập hoặc mật khẩu không chính xác!" : "Username or password invalid!");
            //    }

            //    //if (user.Status != "AC")
            //    //    return new ApiResponse<ApplicationUser>(null, _identityService.GetUserLangId() == "vi" ? "Tài khoản không hợp lệ!" : "Account invalid!");
            //    //if (!user.Active)
            //    //{
            //    //    return new ApiResponse<ApplicationUser>(null, _identityService.GetUserLangId() == "vi" ? "Tài khoản chưa được kích hoạt" : "Your account has not been actived");
            //    //}
            //    //if (user.Status != "A")
            //    //{
            //    //    return new ErrorResponse(ErrorCodes.NotApprove, null, _identityService.GetUserLangId() == "vi" ? "Tài khoản chưa duyệt!" : "Your account has not been Approved");
            //    //}

            //    //if (user.Blocked)
            //    //    return new ErrorResponse("0002", null, _identityService.GetUserLangId() == "vi" ? "Tài khoản đã bị khóa. Liên hệ admin để mở khóa!" : "Your account has been blocked. Please contact admin to unlock!");

            //    //if (user.FailedLoginCount > 0)
            //    //{
            //    //    var u = await _unitOfWork.Repository.GetAsync < User(x => x.UserName == user.UserName);
            //    //    u.FailedLoginCount = 0;
            //    //    await _unitOfWork.SaveEntitiesAsync();
            //    //}

            //    //if (password == user.Password && user.ImeiNumber is null || user.ImeiNumber == "")
            //    //{
            //    //    UpdateImeiNumber(user.UserName, loginForm.Imei ?? "");
            //    //    user = (UserRaw)await GetUser(loginForm.UserName);
            //    //}
            //    /*
            //     * 
            //     * checkImei*/
            //    if (loginForm.Imei != user.ImeiNumber && !user.MultiLogin)
            //    {
            //        return new ErrorResponse("0020", null, _identityService.GetUserLangId() == "vi" ? "Thiết bị không hợp lệ! Liên hệ admin để được hỗ trợ." : "Imei not match. Please contact your admin!");
            //    }
            //    CheckinFirstViewModel checkinInfo = await GetLoginFirst(user.UserName);
            //    var token = await GenergateToken(user.UserName, checkinInfo.BranchID);

            //    var lateTime = await _userQuery.GetLateTimeAsync(DateTime.Now, "LOGIN", "", "");

            //    if (token != null)
            //    {
            //        var result = new object();
            //        if (lateTime == null)
            //        {
            //            result = new
            //            {
            //                access_token = token,
            //                checkInBranchData = checkinInfo.isCheckIn == false ? null : checkinInfo,
            //                IsChangePassword = user.IsFirstLogin
            //            };
            //            return result;
            //        }

            //        result = new
            //        {
            //            access_token = token,
            //            checkInBranchData = checkinInfo.isCheckIn == false ? null : checkinInfo,
            //            dataLoginLate = lateTime.LateTime == "" ? null : lateTime,
            //            IsChangePassword = user.IsFirstLogin
            //        };
            //        return result;
            //    }
            //    return "Error";
            //}
            //catch
            //{
            //    return "Error";
            //}

        }

        //public async Task<object?> CheckLateTime()
        //{
        //    var late = 0;
        //    DateTime lateTime = DateTime.Now;
        //    DateTime visitTime = DateTime.Now;
        //    DateTime policyTime = DateTime.Now;
        //    var checkinTime = await _userQuery.GetLoginTimeAsync();
        //    if (checkinTime.CheckinLate == "1")
        //    {
        //        TimeSpan time = TimeSpan.Parse(checkinTime.CheckinTime);
        //        visitTime = new DateTime().AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute);
        //        policyTime = new DateTime().AddHours(time.Hours).AddMinutes(time.Minutes);
        //        if (DateTime.Compare(visitTime, policyTime) > 0)
        //        {
        //            late = 1;
        //            lateTime = new DateTime().AddHours(visitTime.Hour - policyTime.Hour).AddMinutes(visitTime.Minute - policyTime.Minute);
        //        }
        //    }
        //    var data = new LateTimeResponse(visitTime.ToString("HH:mm"), lateTime.ToString("HH:mm"), policyTime.ToString("HH:mm"));

        //    return checkinTime.CheckinLate == "1" ? data : null;
        //}


        public async Task<ApiResponse<ApplicationUser>> Register(RegisterForm registerForm)
        {
            // Kiểm tra xem người dùng đã tồn tại chưa
            var userExists = await _userManager.FindByNameAsync(registerForm.Username);
            if (userExists != null)
                return new ApiResponse<ApplicationUser>("User already exists!");

            // Tạo người dùng mới
            var user = new ApplicationUser
            {
                UserName = registerForm.Username,
                Email = registerForm.Email,
                EmailConfirmed = true // Có thể thêm logic xác nhận email sau nếu cần
            };

            // Thêm người dùng vào hệ thống
            var result = await _userManager.CreateAsync(user, registerForm.Password);
            if (!result.Succeeded)
            {
                return new ApiResponse<ApplicationUser>(result.Errors);
            }

            // Gán quyền (role) cho người dùng
            if (!await _roleManager.RoleExistsAsync(registerForm.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(registerForm.Role));
            }
            await _userManager.AddToRoleAsync(user, registerForm.Role);

            return new ApiResponse<ApplicationUser>(user, "User created successfully!");
        }

        public async Task<ApiResponse<bool>> ForgetPassword([FromBody] ForgotPasswordRequest model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return new ApiResponse<bool>("User not found" );
            }

            // Tạo token đặt lại mật khẩu
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Tạo URL đặt lại mật khẩu
            var resetUrl = $"{_configuration["ClientUrl"]}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={user.Email}";

            // Gửi email
            await _emailService.SendEmailAsync(user.Email, "Reset your password ", $"'{resetToken}'");

            return new ApiResponse<bool>("Password reset link has been sent to your email.", true);
        }


        public async Task<ApplicationUser> GetUser(string username)
        {

            try
            {

                var user = await _unitOfWork.Repository.GetOneAsync<ApplicationUser>(u=>u.UserName == username);
                return user;

                //using (var connection = new SqlConnection(ConnectionString))
                //{
                //    //LoginForm query = await connection.QueryFirstOrDefaultAsync<UserRaw>(sql);
                //    //return query;


                //}
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //public async Task<CheckinFirstViewModel> GetLoginFirst(string username)
        //{
        //    var sql = $@"exec [dbo].[API_CheckinFirstStatus] '{username}'";

        //    try
        //    {
        //        using (var connection = new SqlConnection(ConnectionString))
        //        {
        //            var query = await connection.QueryFirstOrDefaultAsync<CheckinFirstViewModel>(sql);
        //            return query == null ? new CheckinFirstViewModel() : query;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }
        //}

        //public bool ValidatePassword(string password)
        //{
        //    var sql = $@"select * from Users (nolock) Where UserName = '{_identityService.GetUserName()}' and Password = '{_encryption.Encrypt(password)}'";

        //    try
        //    {
        //        using (var connection = new SqlConnection(ConnectionString))
        //        {
        //            var query = connection.QueryFirstOrDefault<User>(sql);
        //            return query != null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }
        //}
        //public void UpdateImeiNumber(string Username, string ImeiNumber)
        //{
        //    var sql = $@"UPDATE Users
        //                SET ImeiNumber = '{ImeiNumber}'
        //                WHERE UserName = '{Username}'";

        //    try
        //    {
        //        using (var connection = new SqlConnection(ConnectionString))
        //        {
        //            var query = connection.Execute(sql);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }
        //}


    }
}
