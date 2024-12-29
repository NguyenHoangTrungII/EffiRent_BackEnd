//using eSales_SFA_Service.API.ViewModels.DashBoards;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace EffiAP.Application.Auth
{
    public static class ApiKeyAuthentication
    {
        public const string ApiKeyOrBearer = nameof(ApiKeyOrBearer);
        public const string ApiKey = nameof(ApiKey);
    }

    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string ApiKey { get; set; } = "oIXpIq3OQYW2dlpui7Kf_8msy41O9e2CejKOLfLTvVM";
    }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options,
                                           ILoggerFactory logger,
                                           UrlEncoder encoder,
                                           ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("ApiKey", out var apiKeyValues))
            {
                return AuthenticateResult.Fail("Missing API Key");
            }

            var providedApiKey = apiKeyValues.FirstOrDefault();
            var expectedApiKey = Options.ApiKey;

            if (string.IsNullOrEmpty(providedApiKey) || providedApiKey != expectedApiKey)
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            var claims = new[] { new Claim(ClaimTypes.Name, "APIKeyUser") };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
