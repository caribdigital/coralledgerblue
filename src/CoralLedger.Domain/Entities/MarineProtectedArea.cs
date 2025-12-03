using CoralLedger.Domain.Common;
using CoralLedger.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Domain.Entities;

public class MarineProtectedArea : BaseEntity, IAggregateRoot, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? LocalName { get; private set; }
    public string? WdpaId { get; private set; }  // World Database on Protected Areas ID
    public Geometry Boundary { get; private set; } = null!;  // Polygon/MultiPolygon
    public Point Centroid { get; private set; } = null!;
    public double AreaSquareKm { get; private set; }
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
    public ICollection<Reef> Reefs { get; private set; } = new List<Reef>();

    private MarineProtectedArea() { }

    public static MarineProtectedArea Create(
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
