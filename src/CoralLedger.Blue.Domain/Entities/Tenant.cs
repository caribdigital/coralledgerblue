using CoralLedger.Blue.Domain.Common;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a tenant in the multi-tenant system (e.g., different Caribbean nations)
/// </summary>
public class Tenant : BaseEntity, IAggregateRoot, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty; // URL-friendly identifier
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeactivatedAt { get; private set; }
    
    // Regional settings
    public string? RegionCode { get; private set; } // e.g., "BS" for Bahamas, "JM" for Jamaica
    public Geometry? EezBoundary { get; private set; } // Exclusive Economic Zone boundary
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation properties
    public TenantConfiguration? Configuration { get; private set; }
    public TenantBranding? Branding { get; private set; }
    public ICollection<MarineProtectedArea> MarineProtectedAreas { get; private set; } = new List<MarineProtectedArea>();
    public ICollection<ApiClient> ApiClients { get; private set; } = new List<ApiClient>();
    
    private Tenant() { }
    
    public static Tenant Create(
        string name,
        string slug,
        string? regionCode = null,
        string? description = null,
        Geometry? eezBoundary = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name is required", nameof(name));
            
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Tenant slug is required", nameof(slug));
            
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            RegionCode = regionCode,
            Description = description,
            EezBoundary = eezBoundary,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        return tenant;
    }
    
    public void Update(string name, string? description = null)
    {
        Name = name;
        Description = description;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void UpdateEezBoundary(Geometry eezBoundary)
    {
        EezBoundary = eezBoundary;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void Reactivate()
    {
        IsActive = true;
        DeactivatedAt = null;
        ModifiedAt = DateTime.UtcNow;
    }
}
