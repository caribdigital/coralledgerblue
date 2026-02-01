using CoralLedger.Blue.Domain.Common;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a single GPS point recorded during a patrol
/// </summary>
public class PatrolRoutePoint : BaseEntity, IAuditableEntity
{
    public Point Location { get; private set; } = null!;
    public DateTime Timestamp { get; private set; }
    public double? Accuracy { get; private set; } // GPS accuracy in meters
    public double? Altitude { get; private set; } // Altitude in meters
    public double? Speed { get; private set; } // Speed in meters per second
    public double? Heading { get; private set; } // Heading in degrees (0-360)

    // Foreign key
    public Guid PatrolRouteId { get; private set; }
    public PatrolRoute PatrolRoute { get; private set; } = null!;

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private PatrolRoutePoint() { }

    public static PatrolRoutePoint Create(
        Guid patrolRouteId,
        Point location,
        DateTime timestamp,
        double? accuracy = null,
        double? altitude = null,
        double? speed = null,
        double? heading = null)
    {
        return new PatrolRoutePoint
        {
            Id = Guid.NewGuid(),
            PatrolRouteId = patrolRouteId,
            Location = location,
            Timestamp = timestamp,
            Accuracy = accuracy,
            Altitude = altitude,
            Speed = speed,
            Heading = heading,
            CreatedAt = DateTime.UtcNow
        };
    }
}
