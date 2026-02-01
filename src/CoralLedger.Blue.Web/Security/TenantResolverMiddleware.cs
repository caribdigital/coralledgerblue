using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Primitives;

namespace CoralLedger.Blue.Web.Security;

/// <summary>
/// Middleware to resolve the current tenant from the request context
/// Resolution order: 1) API Key -> TenantId, 2) Custom Domain, 3) Subdomain, 4) X-Tenant-Id header
/// </summary>
public class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolverMiddleware> _logger;
    
    public TenantResolverMiddleware(RequestDelegate next, ILogger<TenantResolverMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantRepository tenantRepository)
    {
        var tenantId = await ResolveTenantAsync(context, tenantRepository);
        
        if (tenantId.HasValue)
        {
            tenantContext.SetTenant(tenantId.Value);
            _logger.LogDebug("Tenant resolved: {TenantId}", tenantId.Value);
        }
        else
        {
            _logger.LogDebug("No tenant context resolved for request");
        }
        
        await _next(context);
    }
    
    private async Task<Guid?> ResolveTenantAsync(HttpContext context, ITenantRepository tenantRepository)
    {
        // 1. Try to resolve from authenticated API client (preferred for API requests)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst("TenantId");
            if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var apiTenantId))
            {
                _logger.LogDebug("Tenant resolved from API Key authentication: {TenantId}", apiTenantId);
                return apiTenantId;
            }
        }
        
        // 2. Try to resolve from custom domain
        var host = context.Request.Host.Host;
        if (!string.IsNullOrWhiteSpace(host) && !IsLocalhost(host))
        {
            var tenant = await tenantRepository.GetByDomainAsync(host);
            if (tenant != null)
            {
                _logger.LogDebug("Tenant resolved from custom domain {Domain}: {TenantId}", host, tenant.Id);
                return tenant.Id;
            }
            
            // 3. Try to resolve from subdomain (e.g., bahamas.coralledger.blue)
            var parts = host.Split('.');
            if (parts.Length >= 3) // subdomain.domain.tld
            {
                var subdomain = parts[0];
                tenant = await tenantRepository.GetBySlugAsync(subdomain);
                if (tenant != null)
                {
                    _logger.LogDebug("Tenant resolved from subdomain {Subdomain}: {TenantId}", subdomain, tenant.Id);
                    return tenant.Id;
                }
            }
        }
        
        // 4. Try to resolve from X-Tenant-Id header (fallback for testing/development)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out StringValues tenantIdHeader))
        {
            if (Guid.TryParse(tenantIdHeader.FirstOrDefault(), out var headerTenantId))
            {
                _logger.LogDebug("Tenant resolved from X-Tenant-Id header: {TenantId}", headerTenantId);
                return headerTenantId;
            }
        }
        
        return null;
    }
    
    private static bool IsLocalhost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("127.0.0.1")
            || host.StartsWith("0.0.0.0")
            || host.StartsWith("[::1]");
    }
}

public static class TenantResolverMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolver(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolverMiddleware>();
    }
}
