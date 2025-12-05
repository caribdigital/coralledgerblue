using CoralLedger.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Provides spatial analysis for Marine Protected Area (MPA) proximity.
/// Used for determining MPA context for observations, vessel events, and alerts.
/// </summary>
public interface IMpaProximityService
{
    /// <summary>
    /// Find the nearest MPA to a given point
    /// </summary>
    Task<MpaProximityResult?> FindNearestMpaAsync(Point location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a point is within any MPA
    /// </summary>
    Task<MpaContainmentResult?> CheckMpaContainmentAsync(Point location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all MPAs within a specified radius (in kilometers)
    /// </summary>
    Task<IEnumerable<MpaProximityResult>> FindMpasWithinRadiusAsync(Point location, double radiusKm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get MPA context for a location (whether inside MPA, nearest MPA, protection level)
    /// </summary>
    Task<MpaContext> GetMpaContextAsync(Point location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch operation: Get MPA context for multiple locations
    /// </summary>
    Task<IDictionary<Guid, MpaContext>> GetMpaContextBatchAsync(IEnumerable<(Guid Id, Point Location)> locations, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of finding the nearest MPA to a location
/// </summary>
public record MpaProximityResult
{
    public Guid MpaId { get; init; }
    public string MpaName { get; init; } = string.Empty;
    public ProtectionLevel ProtectionLevel { get; init; }
    public double DistanceKm { get; init; }
    public Point NearestBoundaryPoint { get; init; } = null!;
    public bool IsWithinMpa { get; init; }
}

/// <summary>
/// Result when a location is inside an MPA
/// </summary>
public record MpaContainmentResult
{
    public Guid MpaId { get; init; }
    public string MpaName { get; init; } = string.Empty;
    public ProtectionLevel ProtectionLevel { get; init; }
    public bool IsNoTakeZone { get; init; }
    public double DistanceToNearestBoundaryKm { get; init; }
    public Guid? NearestReefId { get; init; }
    public string? NearestReefName { get; init; }
}

/// <summary>
/// Complete MPA context for a location
/// </summary>
public record MpaContext
{
    public bool IsWithinMpa { get; init; }
    public Guid? CurrentMpaId { get; init; }
    public string? CurrentMpaName { get; init; }
    public ProtectionLevel? CurrentProtectionLevel { get; init; }
    public bool IsNoTakeZone { get; init; }

    // Nearest MPA (may be current if inside, or nearby if outside)
    public Guid? NearestMpaId { get; init; }
    public string? NearestMpaName { get; init; }
    public double? DistanceToNearestMpaKm { get; init; }

    // Reef association
    public Guid? NearestReefId { get; init; }
    public string? NearestReefName { get; init; }
    public double? DistanceToNearestReefKm { get; init; }

    // Alert relevance
    public bool RequiresMpaAlert => IsWithinMpa && (IsNoTakeZone || CurrentProtectionLevel >= ProtectionLevel.NoTake);
    public bool IsNearMpa => DistanceToNearestMpaKm.HasValue && DistanceToNearestMpaKm.Value <= 5.0;
}
