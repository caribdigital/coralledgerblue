using CoralLedger.Blue.Domain.Common;
using CoralLedger.Blue.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a conservation patrol route with GPS tracking
/// Used by conservation officers to document patrol coverage
/// </summary>
public class PatrolRoute : BaseEntity, IAuditableEntity, IAggregateRoot
{
    public string? OfficerName { get; private set; }
    public string? OfficerId { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public PatrolRouteStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public int RecordingIntervalSeconds { get; private set; } = 30; // Default 30 seconds
    
    // Calculated fields
    public double? TotalDistanceMeters { get; private set; }
    public int? DurationSeconds { get; private set; }
    public LineString? RouteGeometry { get; private set; }

    // Spatial context
    public Guid? MarineProtectedAreaId { get; private set; }
    public MarineProtectedArea? MarineProtectedArea { get; private set; }

    // Related collections
    public ICollection<PatrolRoutePoint> Points { get; private set; } = new List<PatrolRoutePoint>();
    public ICollection<PatrolWaypoint> Waypoints { get; private set; } = new List<PatrolWaypoint>();

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private PatrolRoute() { }

    public static PatrolRoute Create(
        string? officerName = null,
        string? officerId = null,
        string? notes = null,
        int recordingIntervalSeconds = 30)
    {
        if (recordingIntervalSeconds < 5 || recordingIntervalSeconds > 300)
            throw new ArgumentOutOfRangeException(nameof(recordingIntervalSeconds), 
                "Recording interval must be between 5 and 300 seconds");

        return new PatrolRoute
        {
            Id = Guid.NewGuid(),
            OfficerName = officerName,
            OfficerId = officerId,
            StartTime = DateTime.UtcNow,
            Status = PatrolRouteStatus.InProgress,
            Notes = notes,
            RecordingIntervalSeconds = recordingIntervalSeconds,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddPoint(PatrolRoutePoint point)
    {
        if (Status != PatrolRouteStatus.InProgress)
            throw new InvalidOperationException("Cannot add points to a patrol that is not in progress");

        Points.Add(point);
        ModifiedAt = DateTime.UtcNow;
        
        // Recalculate route geometry
        RecalculateRouteGeometry();
    }

    public void AddWaypoint(PatrolWaypoint waypoint)
    {
        if (Status != PatrolRouteStatus.InProgress)
            throw new InvalidOperationException("Cannot add waypoints to a patrol that is not in progress");

        Waypoints.Add(waypoint);
        ModifiedAt = DateTime.UtcNow;
    }

    public void Complete(string? completionNotes = null)
    {
        if (Status != PatrolRouteStatus.InProgress)
            throw new InvalidOperationException("Can only complete a patrol that is in progress");

        Status = PatrolRouteStatus.Completed;
        EndTime = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(completionNotes))
        {
            Notes = string.IsNullOrEmpty(Notes) 
                ? completionNotes 
                : $"{Notes}\n\nCompletion Notes: {completionNotes}";
        }

        // Calculate final statistics
        CalculateStatistics();
        RecalculateRouteGeometry();
        
        ModifiedAt = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        if (Status != PatrolRouteStatus.InProgress)
            throw new InvalidOperationException("Can only cancel a patrol that is in progress");

        Status = PatrolRouteStatus.Cancelled;
        EndTime = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(reason))
        {
            Notes = string.IsNullOrEmpty(Notes) 
                ? $"Cancelled: {reason}" 
                : $"{Notes}\n\nCancelled: {reason}";
        }

        ModifiedAt = DateTime.UtcNow;
    }

    public void SetMpaContext(Guid? mpaId)
    {
        MarineProtectedAreaId = mpaId;
        ModifiedAt = DateTime.UtcNow;
    }

    private void CalculateStatistics()
    {
        if (EndTime.HasValue)
        {
            DurationSeconds = (int)(EndTime.Value - StartTime).TotalSeconds;
        }

        if (Points.Count >= 2)
        {
            var orderedPoints = Points.OrderBy(p => p.Timestamp).ToList();
            double totalDistance = 0;

            for (int i = 0; i < orderedPoints.Count - 1; i++)
            {
                var point1 = orderedPoints[i].Location;
                var point2 = orderedPoints[i + 1].Location;
                
                // Calculate distance using Haversine formula
                totalDistance += CalculateDistance(point1, point2);
            }

            TotalDistanceMeters = totalDistance;
        }
    }

    private void RecalculateRouteGeometry()
    {
        if (Points.Count >= 2)
        {
            var orderedPoints = Points
                .OrderBy(p => p.Timestamp)
                .Select(p => p.Location.Coordinate)
                .ToArray();
            
            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            RouteGeometry = factory.CreateLineString(orderedPoints);
        }
    }

    private static double CalculateDistance(Point point1, Point point2)
    {
        // Haversine formula to calculate distance between two GPS coordinates
        const double EarthRadiusMeters = 6371000;
        
        var lat1 = DegreesToRadians(point1.Y);
        var lat2 = DegreesToRadians(point2.Y);
        var deltaLat = DegreesToRadians(point2.Y - point1.Y);
        var deltaLon = DegreesToRadians(point2.X - point1.X);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
