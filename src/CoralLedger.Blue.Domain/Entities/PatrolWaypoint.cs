using CoralLedger.Blue.Domain.Common;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a waypoint with notes added during a patrol
/// Used to mark points of interest, incidents, or observations
/// </summary>
public class PatrolWaypoint : BaseEntity, IAuditableEntity
{
    public Point Location { get; private set; } = null!;
    public DateTime Timestamp { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string? WaypointType { get; private set; } // e.g., "Incident", "Observation", "Checkpoint"

    // Foreign key
    public Guid PatrolRouteId { get; private set; }
    public PatrolRoute PatrolRoute { get; private set; } = null!;

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private PatrolWaypoint() { }

    public static PatrolWaypoint Create(
        Guid patrolRouteId,
        Point location,
        DateTime timestamp,
        string title,
        string? notes = null,
        string? waypointType = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Waypoint title cannot be empty", nameof(title));

        return new PatrolWaypoint
        {
            Id = Guid.NewGuid(),
            PatrolRouteId = patrolRouteId,
            Location = location,
            Timestamp = timestamp,
            Title = title,
            Notes = notes,
            WaypointType = waypointType,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateNotes(string notes)
    {
        Notes = notes;
        ModifiedAt = DateTime.UtcNow;
    }
}
