using NetTopologySuite.Geometries;

namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Provides optimized batch point-in-polygon containment checks for large datasets.
/// Implements Sprint 3.3 US-3.3.2: Optimize point-in-polygon queries (&lt;100ms for 10K positions).
///
/// Performance optimizations:
/// 1. Uses PreparedGeometry for cached, repeated containment checks
/// 2. Applies bounding box pre-filtering before detailed checks
/// 3. Batches points for efficient processing
/// 4. Caches MPA boundaries in memory for quick access
/// </summary>
public interface IBatchContainmentService
{
    /// <summary>
    /// Check containment for multiple points against all MPAs in a single batch operation.
    /// Returns a dictionary mapping each point to its containing MPA (if any).
    /// Target: &lt;100ms for 10K positions.
    /// </summary>
    /// <param name="points">Collection of points to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of point index to containing MPA info (null if outside all MPAs)</returns>
    Task<IDictionary<int, MpaContainmentInfo?>> CheckContainmentBatchAsync(
        IReadOnlyList<Point> points,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check containment for multiple points against a specific MPA boundary.
    /// </summary>
    /// <param name="points">Collection of points to check</param>
    /// <param name="mpaId">ID of the MPA to check against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of point indices that are contained within the MPA</returns>
    Task<IReadOnlyList<int>> CheckContainmentInMpaAsync(
        IReadOnlyList<Point> points,
        Guid mpaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find which MPAs intersect with a given bounding box (for map viewport queries).
    /// Uses GiST index for efficient spatial filtering.
    /// </summary>
    /// <param name="minLon">Minimum longitude</param>
    /// <param name="minLat">Minimum latitude</param>
    /// <param name="maxLon">Maximum longitude</param>
    /// <param name="maxLat">Maximum latitude</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of MPA IDs that intersect the bounding box</returns>
    Task<IReadOnlyList<Guid>> FindMpasInBoundingBoxAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-warm the MPA boundary cache for optimal query performance.
    /// Call this during application startup or when MPAs are updated.
    /// </summary>
    Task WarmCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the MPA boundary cache.
    /// Call when MPA boundaries are updated.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Information about an MPA containment result.
/// </summary>
public record MpaContainmentInfo
{
    /// <summary>
    /// The MPA's unique identifier.
    /// </summary>
    public required Guid MpaId { get; init; }

    /// <summary>
    /// The MPA's name.
    /// </summary>
    public required string MpaName { get; init; }

    /// <summary>
    /// The MPA's protection level.
    /// </summary>
    public required string ProtectionLevel { get; init; }

    /// <summary>
    /// Whether this is a no-take zone.
    /// </summary>
    public bool IsNoTakeZone { get; init; }
}
