using CoralLedger.Blue.Application.Common.Interfaces;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Provides access to the current tenant context for the request
/// </summary>
public class TenantContext : ITenantContext
{
    private Guid? _tenantId;
    private string? _tenantSlug;
    
    public Guid? TenantId => _tenantId;
    public string? TenantSlug => _tenantSlug;
    public bool HasTenant => _tenantId.HasValue;
    
    public void SetTenant(Guid tenantId, string? tenantSlug = null)
    {
        _tenantId = tenantId;
        _tenantSlug = tenantSlug;
    }
}
