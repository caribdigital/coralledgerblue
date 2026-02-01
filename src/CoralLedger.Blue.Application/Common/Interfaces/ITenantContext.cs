namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current tenant context
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID. Returns null if no tenant context is available.
    /// </summary>
    Guid? TenantId { get; }
    
    /// <summary>
    /// Gets the current tenant slug (URL-friendly identifier)
    /// </summary>
    string? TenantSlug { get; }
    
    /// <summary>
    /// Indicates if a tenant context is available
    /// </summary>
    bool HasTenant { get; }
    
    /// <summary>
    /// Sets the tenant context for the current request
    /// </summary>
    void SetTenant(Guid tenantId, string? tenantSlug = null);
}
