using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CoralLedger.Blue.Web.Security;

/// <summary>
/// Custom AuthenticationStateProvider that provides authentication state from server-side JWT cookie
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        // Get the authenticated user from HttpContext (set by JWT/Cookie authentication middleware)
        var user = httpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        
        return Task.FromResult(new AuthenticationState(user));
    }
}
