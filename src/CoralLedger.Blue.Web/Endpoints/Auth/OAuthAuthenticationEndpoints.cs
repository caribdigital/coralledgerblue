using System.Security.Claims;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Web.Endpoints.Auth;

public static class OAuthAuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapOAuthAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapGet("/signin-google", async (HttpContext httpContext) =>
            {
                var properties = new AuthenticationProperties
                {
                    RedirectUri = "/api/auth/oauth-callback?provider=Google"
                };
                return Results.Challenge(properties, new[] { "Google" });
            })
            .WithName("SignInGoogle")
            .WithSummary("Initiate Google OAuth login");

        group.MapGet("/signin-microsoft", async (HttpContext httpContext) =>
            {
                var properties = new AuthenticationProperties
                {
                    RedirectUri = "/api/auth/oauth-callback?provider=Microsoft"
                };
                return Results.Challenge(properties, new[] { "Microsoft" });
            })
            .WithName("SignInMicrosoft")
            .WithSummary("Initiate Microsoft OAuth login");

        group.MapGet("/oauth-callback", HandleOAuthCallback)
            .WithName("OAuthCallback")
            .WithSummary("Handle OAuth provider callback")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> HandleOAuthCallback(
        [FromQuery] string provider,
        HttpContext httpContext,
        MarineDbContext context,
        IJwtTokenService jwtTokenService,
        ITenantContext tenantContext,
        ILoggerFactory loggerFactory)
    {
        try
        {
            // Authenticate with the OAuth provider
            var authenticateResult = await httpContext.AuthenticateAsync(provider);

            if (!authenticateResult.Succeeded)
            {
                return Results.Redirect("/login?error=oauth_failed");
            }

            var claims = authenticateResult.Principal?.Claims;
            if (claims == null)
            {
                return Results.Redirect("/login?error=no_claims");
            }

            // Extract user information from OAuth claims
            var emailClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            var nameClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            var nameIdentifierClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            if (emailClaim == null || nameIdentifierClaim == null)
            {
                return Results.Redirect("/login?error=missing_email");
            }

            var email = emailClaim.Value.ToLowerInvariant();
            var fullName = nameClaim?.Value;
            var oauthSubjectId = nameIdentifierClaim.Value;

            // Determine tenant - use default tenant for OAuth (can be enhanced later)
            var tenantId = tenantContext.TenantId ?? await GetDefaultTenantId(context);
            if (!tenantId.HasValue)
            {
                return Results.Redirect("/login?error=no_tenant");
            }

            // Check if user already exists by email or OAuth provider
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => 
                    (u.Email == email && u.TenantId == tenantId.Value) ||
                    (u.OAuthProvider == provider && u.OAuthSubjectId == oauthSubjectId));

            if (user == null)
            {
                // Create new user for first-time OAuth login
                user = TenantUser.Create(
                    tenantId.Value,
                    email,
                    fullName,
                    role: "User");

                // Set OAuth provider information
                user.SetOAuthProvider(provider, oauthSubjectId);
                
                // Mark email as confirmed since OAuth provider verified it
                user.ConfirmEmail();

                context.TenantUsers.Add(user);
                await context.SaveChangesAsync();
            }
            else
            {
                // Link OAuth provider to existing user if not already linked
                if (string.IsNullOrEmpty(user.OAuthProvider))
                {
                    user.SetOAuthProvider(provider, oauthSubjectId);
                    
                    // Mark email as confirmed since OAuth provider verified it
                    if (!user.EmailConfirmed)
                    {
                        user.ConfirmEmail();
                    }
                    
                    await context.SaveChangesAsync();
                }

                // Check if user is active
                if (!user.IsActive)
                {
                    return Results.Redirect("/login?error=account_disabled");
                }

                // Check if account is locked
                if (user.IsLockedOut())
                {
                    return Results.Redirect("/login?error=account_locked");
                }
            }

            // Record successful login
            user.RecordLogin();
            await context.SaveChangesAsync();

            // Generate tokens
            var accessToken = jwtTokenService.GenerateAccessToken(user);
            var refreshToken = jwtTokenService.GenerateRefreshToken();

            // Sign in with cookie authentication for Blazor
            await SignInWithCookie(httpContext, user, accessToken);

            // Redirect to dashboard after successful login
            return Results.Redirect("/");
        }
        catch (Exception ex)
        {
            // Log the exception with proper logging infrastructure
            var logger = loggerFactory.CreateLogger("OAuthAuthentication");
            logger.LogError(ex, "OAuth callback error for provider {Provider}", provider);
            return Results.Redirect("/login?error=oauth_error");
        }
    }

    /// <summary>
    /// Gets the default tenant ID for OAuth logins
    /// </summary>
    private static async Task<Guid?> GetDefaultTenantId(MarineDbContext context)
    {
        // Get the first active tenant as default
        var tenant = await context.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        return tenant?.Id;
    }

    /// <summary>
    /// Signs in the user with cookie authentication
    /// </summary>
    private static async Task SignInWithCookie(HttpContext httpContext, TenantUser user, string accessToken)
    {
        // Create claims principal from user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("TenantId", user.TenantId.ToString())
        };

        if (!string.IsNullOrEmpty(user.FullName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.FullName));
        }

        if (!string.IsNullOrEmpty(user.OAuthProvider))
        {
            claims.Add(new Claim("OAuthProvider", user.OAuthProvider));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Sign in with cookie (HttpOnly, Secure in production)
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true, // Cookie persists across browser sessions
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) // Match JWT expiration
            });
    }
}
