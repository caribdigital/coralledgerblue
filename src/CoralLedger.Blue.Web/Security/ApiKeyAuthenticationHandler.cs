using System.Security.Claims;
using System.Text.Encodings.Web;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoralLedger.Blue.Web.Security;

/// <summary>
/// Authentication handler for API key-based authentication
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if API key is provided in header
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var apiKey = await _apiKeyService.ValidateApiKeyAsync(providedApiKey, Context.RequestAborted)
                .ConfigureAwait(false);

            if (apiKey == null)
            {
                return AuthenticateResult.Fail("Invalid API key");
            }

            // Update last used (fire and forget to avoid slowing down requests)
            // Note: We intentionally don't await this to keep authentication fast
            // Errors are logged but don't fail the authentication
            _ = Task.Run(async () =>
            {
                try
                {
                    await _apiKeyService.UpdateApiKeyLastUsedAsync(apiKey.Id, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to update API key last used timestamp for key {ApiKeyId}", apiKey.Id);
                }
            }, CancellationToken.None);

            // Create claims
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, apiKey.ApiClientId.ToString()),
                new Claim(ClaimTypes.Name, apiKey.ApiClient.Name),
                new Claim("ApiKeyId", apiKey.Id.ToString()),
                new Claim("ClientId", apiKey.ApiClient.ClientId),
                new Claim("Scopes", apiKey.Scopes),
                new Claim("RateLimit", apiKey.ApiClient.RateLimitPerMinute.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating API key");
            return AuthenticateResult.Fail("Error validating API key");
        }
    }
}

/// <summary>
/// Options for API key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
}
