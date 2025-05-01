using EffiAP.Application;
using EffiAP.Application.Configs.FluentValidation;
using EffiAP.Infrastructure;
using EffiAP.Presentation;
using MediatR;
using Serilog;
using Newtonsoft.Json;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using EffiAP.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using EffiAP.Infrastructure.EntityModels;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
//using EffiAP.Application.Services.Messaging;
using EffiRent.Application.Services.Rabbit;
using EffiHR.Infrastructure.Data;
using EffiHR.Infrastructure.Middlewares;
using Microsoft.Extensions.Options;
using EffiRent.Domain.ViewModels.Users;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Antiforgery;
using RabbitMQ.Client;
using EffiRent.Application.Services.Rabbit;
using EffiAP.Application.Services.Redis;
using StackExchange.Redis;
using FluentAssertions.Common;
using Quartz;
using EffiRent.WebAPI.Jobs;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<EffiRentContext>()
    .AddDefaultTokenProviders();

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Cấu hình Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EffiRentContext>(options =>
    options.UseSqlServer(connectionString));

// Đăng ký MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

// Cấu hình FluentValidation
ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;

// Cấu hình gửi mail
builder.Services.Configure<MailSetting>(builder.Configuration.GetSection("EmailSettings"));

//Use Scrutor and register service lifetime for Interface
builder.Services.Scan(scan => scan
    .FromAssemblies(AssemblyHelper.GetAllAssemblies())
    //.FromAssemblyOf<IInjectableService>()
    .AddClasses(classes => classes.AssignableTo<ITransientService>())
    .AsImplementedInterfaces()
    .WithTransientLifetime()

    .AddClasses(classes => classes.AssignableTo<IScopedService>())
    .AsImplementedInterfaces()
    .WithScopedLifetime()

    .AddClasses(classes => classes.AssignableTo<ISingletonService>())
    .AsImplementedInterfaces()
    .WithSingletonLifetime()

    .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime()
);

// Cấu hình Rate Limiting
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "10s",
            Limit = 5
        }
    };
});
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddMemoryCache();
builder.Services.AddInMemoryRateLimiting();

// Cấu hình bảo vệ CSRF
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Đăng ký các dịch vụ
builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddPresentation();

// Cấu hình Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Cấu hình JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Cấu hình Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký RabbitMQ Consumer
builder.Services.AddHostedService<RabbitMQConsumer>();

// Cấu hình Redis Cache
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "EffiRentCache";
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect("localhost:6379"));
//builder.Services.AddSingleton<IRedisService, >();

// Cấu hình Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Đăng ký IConnectionFactory
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ConnectionFactory
    {
        HostName = config["RabbitMQ:HostName"] ?? "localhost",
        Port = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
        UserName = config["RabbitMQ:UserName"] ?? "guest",
        Password = config["RabbitMQ:Password"] ?? "guest",
        DispatchConsumersAsync = true // Hỗ trợ AsyncEventingBasicConsumer
    };
});

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("CheckExpiringContractsJob");
    q.AddJob<CheckExpiringContractsJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("CheckExpiringContractsTrigger")
        .WithDailyTimeIntervalSchedule(s => s
            .WithIntervalInHours(24)
            .OnEveryDay()
            .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(0, 0)) // Chạy lúc 0:00 UTC
        ));
});

// Thêm Quartz hosted service để chạy scheduler
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Thêm các dịch vụ khác (MediatR, DbContext, v.v.)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CheckExpiringContractsJob).Assembly));

var app = builder.Build();

// Cấu hình Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

// Middleware bảo vệ CSRF
app.Use(async (context, next) =>
{
    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
        new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict });

    await next();
});

// Middleware Content Security Policy (CSP) chống XSS
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'");
    await next();
});

// Middleware xử lý lỗi Unauthorized
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
    {
        context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
        var unAuthMess = context.Request.Headers["LangID"] == "vi" ?
            "Phiên đăng nhập đã hết hạn, vui lòng tắt ứng dụng và đăng nhập lại." :
            "Token expired! Please turn off and re-open app.";
        await context.Response.WriteAsync(JsonConvert.SerializeObject(new { message = unAuthMess }));
    }
});

app.UseIpRateLimiting();  // Kích hoạt Rate Limiting
app.UseCors();
app.UseSession();
app.UseMiddleware<SessionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseFluentValidationExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseEndpoints(endpoints => endpoints.MapControllers());

app.Run();


//using EffiAP.Application;
//using EffiAP.Application.Configs.FluentValidation;
//using EffiAP.Infrastructure;
//using EffiAP.Presentation;
//using EffiAP.Application.Configs.FluentValidation;
//using MediatR;
//using Serilog;
//using Newtonsoft.Json;
//using System.Net;
//using System.Text.Encodings.Web;
//using System.Text.Json;
//using System.Text;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.IdentityModel.Tokens;
//using FluentValidation;
//using EffiAP.Domain.SeedWork;
//using Microsoft.EntityFrameworkCore;
//using EffiAP.Infrastructure.EntityModels;
//using System.Reflection;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.AspNetCore.Identity;
//using EffiAP.Application.Services.Messaging;
//using EffiHR.Infrastructure.Data;
//using FluentAssertions.Common;
//using EffiHR.Infrastructure.Middlewares;
//using Microsoft.Extensions.Options;
//using EffiRent.Domain.ViewModels.Users;



//var builder = WebApplication.CreateBuilder(args);


////builder.Services.AddEndpointsApiExplorer();
////builder.Services.AddSwaggerGen();



//#region Inject Services

//builder.Services.AddIdentity<IdentityUser, IdentityRole>()
//    .AddEntityFrameworkStores<EffiRentContext>()
//    .AddDefaultTokenProviders();


//builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));


//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
//builder.Services.AddDbContext<EffiRentContext>(options =>
//    options.UseSqlServer(connectionString));


////Add Mediator
//builder.Services.AddMediatR(cfg =>
//{
//    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
//});

////Add FluentValidation    
//ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;

////Mail setting
//builder.Services.Configure<MailSetting>(builder.Configuration.GetSection("EmailSettings"));


////Use Scrutor and register service lifetime for Interface
//builder.Services.Scan(scan => scan
//    .FromAssemblies(AssemblyHelper.GetAllAssemblies())
//    //.FromAssemblyOf<IInjectableService>()
//    .AddClasses(classes => classes.AssignableTo<ITransientService>())
//    .AsImplementedInterfaces()
//    .WithTransientLifetime()

//    .AddClasses(classes => classes.AssignableTo<IScopedService>())
//    .AsImplementedInterfaces()
//    .WithScopedLifetime()

//    .AddClasses(classes => classes.AssignableTo<ISingletonService>())
//    .AsImplementedInterfaces()
//    .WithSingletonLifetime()

//    .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
//    .AsImplementedInterfaces()
//    .WithScopedLifetime()
//);

//#endregion



//builder.Services
//    .AddApplication()
//    .AddInfrastructure()
//    .AddPresentation();

//builder.Host.UseSerilog((context, configuration) =>
//configuration.ReadFrom.Configuration(context.Configuration));




//#region Config Authenticaton
////builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
////{
////    options.RequireHttpsMetadata = false;
////    options.SaveToken = true;
////    options.TokenValidationParameters = new TokenValidationParameters()
////    {
////        ValidateIssuer = true,
////        ValidateAudience = true,
////        ValidAudience = builder.Configuration["Jwt:Audience"],
////        ValidIssuer = builder.Configuration["Jwt:Issuer"],
////        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
////    };
////});
////builder.Services.AddAuthentication(ApiKeyAuthentication.ApiKey)
////        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthentication.ApiKey, null);
////builder.Services.AddAuthorization(options =>
////{
////    options.AddPolicy(ApiKeyAuthentication.ApiKeyOrBearer, policy =>
////    {
////        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthentication.ApiKey);
////        policy.RequireAuthenticatedUser();
////    });
////});
//#endregion

//#region Add Swagger
//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
//#endregion


//#region Thirty Party
//builder.Services.AddHostedService<RabbitMQConsumer>();  // Đăng ký RabbitMQ Consumer

//var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
//builder.Services.AddStackExchangeRedisCache(options =>
//{
//    options.Configuration = redisConnectionString;
//    options.InstanceName = "";
//});

//#endregion

//#region Session field
//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian session tồn tại
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true; // Đảm bảo cookie này là cần thiết
//});
//#endregion

//var app = builder.Build();

//app.UseSwagger();
//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
//});

//app.Use(async (context, next) =>
//{
//    await next();

//    if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
//    {
//        context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
//        var options3 = new JsonSerializerOptions
//        {
//            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
//            WriteIndented = true
//        };
//        var unAuthMess = context.Request.Headers["LangID"] == "vi" ? "Phiên đăng nhập đã hết hạn, vui lòng tắt ứng dụng và đăng nhập lại." : "Token expired! Please turn off and re-open app.";
//        byte[] bytes = Encoding.Default.GetBytes(unAuthMess);

//        await context.Response.WriteAsync(JsonConvert.SerializeObject(new
//        {
//            message = Encoding.UTF8.GetString(bytes)
//        }));
//    }
//});

//app.UseCors();

////app.UseHttpsRedirection();

//app.UseRouting();


//app.UseSession();

//app.UseMiddleware<SessionMiddleware>();


//app.UseAuthentication();

//app.UseAuthorization();

//app.UseFluentValidationExceptionHandler();

//app.UseRouting();

//app.UseHttpsRedirection();

//app.UseEndpoints(endpoints =>
//{
//    endpoints.MapControllers();
//});

//app.Run();
