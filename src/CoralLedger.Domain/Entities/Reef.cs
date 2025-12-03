using CoralLedger.Domain.Common;
using CoralLedger.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Domain.Entities;

public class Reef : BaseEntity, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public Geometry Location { get; private set; } = null!;  // Point, LineString, or Polygon
    public ReefHealth HealthStatus { get; private set; }
    public double? DepthMeters { get; private set; }
    public double? LengthKm { get; private set; }
    public DateOnly? LastSurveyDate { get; private set; }
    public double? CoralCoverPercentage { get; private set; }
    public double? BleachingPercentage { get; private set; }

    // Foreign key
    public Guid? MarineProtectedAreaId { get; private set; }
    public MarineProtectedArea? MarineProtectedArea { get; private set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Reef() { }

    public static Reef Create(
        string name,
        Geometry location,
        ReefHealth healthStatus = ReefHealth.Unknown,
        double? depthMeters = null,
        Guid? marineProtectedAreaId = null)
    {
        return new Reef
        {
            Id = Guid.NewGuid(),
            Name = name,
            Location = location,
            HealthStatus = healthStatus,
            DepthMeters = depthMeters,
            MarineProtectedAreaId = marineProtectedAreaId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateHealth(ReefHealth health, double? coralCover, double? bleaching)
    {
        HealthStatus = health;
        CoralCoverPercentage = coralCover;
        BleachingPercentage = bleaching;
        LastSurveyDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ModifiedAt = DateTime.UtcNow;
    }

    public void AssignToMpa(Guid mpaId)
    {
        MarineProtectedAreaId = mpaId;
        ModifiedAt = DateTime.UtcNow;
    }
}
