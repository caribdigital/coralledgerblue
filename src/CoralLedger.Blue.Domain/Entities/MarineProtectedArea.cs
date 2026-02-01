using CoralLedger.Blue.Domain.Common;
using CoralLedger.Blue.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Domain.Entities;

public class MarineProtectedArea : BaseEntity, IAggregateRoot, IAuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; private set; } = string.Empty;
    public string? LocalName { get; private set; }
    public string? WdpaId { get; private set; }  // World Database on Protected Areas ID
    public Geometry Boundary { get; private set; } = null!;  // Polygon/MultiPolygon
    public Geometry? BoundarySimplifiedDetail { get; private set; }  // ~0.0001° tolerance (~10m)
    public Geometry? BoundarySimplifiedMedium { get; private set; }  // ~0.001° tolerance (~100m)
    public Geometry? BoundarySimplifiedLow { get; private set; }     // ~0.01° tolerance (~1km)
    public Point Centroid { get; private set; } = null!;
    public double AreaSquareKm { get; private set; }
    public DateTime? WdpaLastSync { get; private set; }
    public MpaStatus Status { get; private set; }
    public ProtectionLevel ProtectionLevel { get; private set; }
    public IslandGroup IslandGroup { get; private set; }
    public DateOnly? DesignationDate { get; private set; }
    public string? ManagingAuthority { get; private set; }
    public string? Description { get; private set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    // Navigation properties
    public Tenant? Tenant { get; private set; }
    public ICollection<Reef> Reefs { get; private set; } = new List<Reef>();

    private MarineProtectedArea() { }

    public static MarineProtectedArea Create(
        Guid tenantId,
        string name,
        Geometry boundary,
        ProtectionLevel protectionLevel,
        IslandGroup islandGroup,
        string? wdpaId = null,
        string? description = null,
        string? managingAuthority = null,
        DateOnly? designationDate = null)
    {
        var mpa = new MarineProtectedArea
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Boundary = boundary,
            Centroid = boundary.Centroid,
            AreaSquareKm = CalculateAreaSquareKm(boundary),
            ProtectionLevel = protectionLevel,
            IslandGroup = islandGroup,
            Status = MpaStatus.Active,
            WdpaId = wdpaId,
            Description = description,
            ManagingAuthority = managingAuthority,
            DesignationDate = designationDate,
            CreatedAt = DateTime.UtcNow
        };
        return mpa;
    }

    public void UpdateDescription(string description)
    {
        Description = description;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateProtectionLevel(ProtectionLevel level)
    {
        ProtectionLevel = level;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Decommission()
    {
        Status = MpaStatus.Decommissioned;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update the MPA boundary with authoritative data from WDPA
    /// </summary>
    public void UpdateBoundaryFromWdpa(Geometry boundary)
    {
        Boundary = boundary;
        Centroid = boundary.Centroid as Point ?? Centroid;
        AreaSquareKm = CalculateAreaSquareKm(boundary);
        WdpaLastSync = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Set pre-computed simplified geometry versions for map performance.
    /// 4-tier system: Full (original) -> Detail -> Medium -> Low
    /// </summary>
    /// <param name="detail">Simplified with ~0.0001° tolerance (~10m at 25°N)</param>
    /// <param name="medium">Simplified with ~0.001° tolerance (~100m at 25°N)</param>
    /// <param name="low">Simplified with ~0.01° tolerance (~1km at 25°N)</param>
    public void SetSimplifiedBoundaries(Geometry? detail, Geometry? medium, Geometry? low)
    {
        BoundarySimplifiedDetail = detail;
        BoundarySimplifiedMedium = medium;
        BoundarySimplifiedLow = low;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update the WDPA ID for this MPA
    /// </summary>
    public void SetWdpaId(string wdpaId)
    {
        WdpaId = wdpaId;
        ModifiedAt = DateTime.UtcNow;
    }

    private static double CalculateAreaSquareKm(Geometry boundary)
    {
        // For SRID 4326 (WGS84), area is in square degrees
        // This is a rough approximation - for production use a proper projection
        // At the equator, 1 degree ≈ 111 km
        var areaInSquareDegrees = boundary.Area;
        var kmPerDegree = 111.0;
        return areaInSquareDegrees * kmPerDegree * kmPerDegree;
    }
}
