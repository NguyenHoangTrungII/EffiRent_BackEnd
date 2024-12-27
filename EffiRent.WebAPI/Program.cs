using EffiAP.Application;
using EffiAP.Application.Configs.FluentValidation;
using EffiAP.Infrastructure;
using EffiAP.Presentation;
using EffiAP.Application.Configs.FluentValidation;
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
using EffiAP.Application.Services.Messaging;
using EffiHR.Infrastructure.Data;
using FluentAssertions.Common;
using EffiHR.Infrastructure.Middlewares;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Options;



var builder = WebApplication.CreateBuilder(args);


//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();



#region Inject Services

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<EffiAPContext>()
    .AddDefaultTokenProviders();


builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EffiAPContext>(options =>
    options.UseSqlServer(connectionString));


//Add Mediator
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

//Add FluentValidation    
ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;

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

#endregion



builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddPresentation();

builder.Host.UseSerilog((context, configuration) =>
configuration.ReadFrom.Configuration(context.Configuration));




#region Config Authenticaton
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
//{
//    options.RequireHttpsMetadata = false;
//    options.SaveToken = true;
//    options.TokenValidationParameters = new TokenValidationParameters()
//    {
//        ValidateIssuer = true,
//        ValidateAudience = true,
//        ValidAudience = builder.Configuration["Jwt:Audience"],
//        ValidIssuer = builder.Configuration["Jwt:Issuer"],
//        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
//    };
//});
//builder.Services.AddAuthentication(ApiKeyAuthentication.ApiKey)
//        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthentication.ApiKey, null);
//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy(ApiKeyAuthentication.ApiKeyOrBearer, policy =>
//    {
//        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthentication.ApiKey);
//        policy.RequireAuthenticatedUser();
//    });
//});
#endregion

#region Add Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion


#region Thirty Party
builder.Services.AddHostedService<RabbitMQConsumer>();  // Đăng ký RabbitMQ Consumer

var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "";
});

#endregion

#region Session field
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian session tồn tại
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Đảm bảo cookie này là cần thiết
});
#endregion

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
    {
        context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";
        var options3 = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        var unAuthMess = context.Request.Headers["LangID"] == "vi" ? "Phiên đăng nhập đã hết hạn, vui lòng tắt ứng dụng và đăng nhập lại." : "Token expired! Please turn off and re-open app.";
        byte[] bytes = Encoding.Default.GetBytes(unAuthMess);

        await context.Response.WriteAsync(JsonConvert.SerializeObject(new
        {
            message = Encoding.UTF8.GetString(bytes)
        }));
    }
});

app.UseCors();

//app.UseHttpsRedirection();

app.UseRouting();


app.UseSession();

app.UseMiddleware<SessionMiddleware>();


app.UseAuthentication();

app.UseAuthorization();

app.UseFluentValidationExceptionHandler();

app.UseRouting();

app.UseHttpsRedirection();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
