using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Provides spatial analysis for Marine Protected Area proximity.
/// Uses ISpatialCalculator for accurate distance calculations with UTM Zone 18N (SRID 32618).
/// Implements Dr. Thorne's GIS Rule 1: SRID 4326 for storage, 32618 for calculations.
/// </summary>
public class MpaProximityService : IMpaProximityService
{
    private readonly MarineDbContext _context;
    private readonly ILogger<MpaProximityService> _logger;
    private readonly ICacheService _cache;
    private readonly ISpatialCalculator _spatialCalculator;

    private const string CachePrefix = "mpa_proximity_";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(15);

    public MpaProximityService(
        MarineDbContext context,
        ILogger<MpaProximityService> logger,
        ICacheService cache,
        ISpatialCalculator spatialCalculator)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _spatialCalculator = spatialCalculator;
    }

    public async Task<MpaProximityResult?> FindNearestMpaAsync(
        Point location,
        CancellationToken cancellationToken = default)
    {
        // Query all MPAs and find the nearest one using PostGIS distance
        var mpas = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ProtectionLevel,
                m.Boundary
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (!mpas.Any())
            return null;

        // Calculate accurate distances using ISpatialCalculator (UTM Zone 18N)
        var mpaWithDistance = mpas
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ProtectionLevel,
                m.Boundary,
                DistanceKm = _spatialCalculator.CalculateDistanceToGeometryKm(location, m.Boundary)
            })
            .OrderBy(m => m.DistanceKm)
            .First();

        var isWithin = mpaWithDistance.Boundary.Contains(location);

        // Find the nearest point on the MPA boundary
        var nearestPoint = mpaWithDistance.Boundary.Boundary is Geometry boundary
            ? FindNearestPointOnBoundary(location, boundary)
            : location;

        return new MpaProximityResult
        {
            MpaId = mpaWithDistance.Id,
            MpaName = mpaWithDistance.Name,
            ProtectionLevel = mpaWithDistance.ProtectionLevel,
            DistanceKm = isWithin ? 0 : mpaWithDistance.DistanceKm,
            NearestBoundaryPoint = nearestPoint,
            IsWithinMpa = isWithin
        };
    }

    public async Task<MpaContainmentResult?> CheckMpaContainmentAsync(
        Point location,
        CancellationToken cancellationToken = default)
    {
        // Find which MPA contains this point (if any)
        var containingMpa = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Where(m => m.Boundary.Contains(location))
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ProtectionLevel,
                m.Boundary
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (containingMpa == null)
            return null;

        // Calculate accurate distance to boundary using ISpatialCalculator (UTM Zone 18N)
        var distanceToBoundaryKm = containingMpa.Boundary.Boundary is Geometry boundary
            ? _spatialCalculator.CalculateDistanceToGeometryKm(location, boundary)
            : 0.0;

        // Find nearest reef within the same MPA
        var reefsInMpa = await _context.Reefs
            .AsNoTracking()
            .Where(r => r.MarineProtectedAreaId == containingMpa.Id)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Location
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var nearestReef = reefsInMpa
            .Select(r => new
            {
                r.Id,
                r.Name,
                DistanceKm = _spatialCalculator.CalculateDistanceKm(location, (Point)r.Location)
            })
            .OrderBy(r => r.DistanceKm)
            .FirstOrDefault();

        return new MpaContainmentResult
        {
            MpaId = containingMpa.Id,
            MpaName = containingMpa.Name,
            ProtectionLevel = containingMpa.ProtectionLevel,
            IsNoTakeZone = containingMpa.ProtectionLevel == ProtectionLevel.NoTake,
            DistanceToNearestBoundaryKm = distanceToBoundaryKm,
            NearestReefId = nearestReef?.Id,
            NearestReefName = nearestReef?.Name
        };
    }

    public async Task<IEnumerable<MpaProximityResult>> FindMpasWithinRadiusAsync(
        Point location,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        // Get all MPAs and calculate accurate distances
        var mpas = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ProtectionLevel,
                m.Boundary
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Calculate accurate distances using ISpatialCalculator and filter by radius
        return mpas
            .Select(m =>
            {
                var distanceKm = _spatialCalculator.CalculateDistanceToGeometryKm(location, m.Boundary);
                var isWithin = m.Boundary.Contains(location);
                var nearestPoint = m.Boundary.Boundary is Geometry boundary
                    ? FindNearestPointOnBoundary(location, boundary)
                    : location;

                return new MpaProximityResult
                {
                    MpaId = m.Id,
                    MpaName = m.Name,
                    ProtectionLevel = m.ProtectionLevel,
                    DistanceKm = isWithin ? 0 : distanceKm,
                    NearestBoundaryPoint = nearestPoint,
                    IsWithinMpa = isWithin
                };
            })
            .Where(r => r.DistanceKm <= radiusKm)
            .OrderBy(r => r.DistanceKm);
    }

    public async Task<MpaContext> GetMpaContextAsync(
        Point location,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"{CachePrefix}{location.X:F6}_{location.Y:F6}";
        var cached = await _cache.GetAsync<MpaContext>(cacheKey).ConfigureAwait(false);
        if (cached != null)
            return cached;

        // Check if inside an MPA
        var containment = await CheckMpaContainmentAsync(location, cancellationToken).ConfigureAwait(false);

        // Find nearest MPA (even if inside one, to get boundary distance)
        var nearestMpa = await FindNearestMpaAsync(location, cancellationToken).ConfigureAwait(false);

        // Find nearest reef regardless of MPA using accurate calculations
        var reefs = await _context.Reefs
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Location
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var nearestReef = reefs
            .Select(r => new
            {
                r.Id,
                r.Name,
                DistanceKm = _spatialCalculator.CalculateDistanceKm(location, (Point)r.Location)
            })
            .OrderBy(r => r.DistanceKm)
            .FirstOrDefault();

        var context = new MpaContext
        {
            IsWithinMpa = containment != null,
            CurrentMpaId = containment?.MpaId,
            CurrentMpaName = containment?.MpaName,
            CurrentProtectionLevel = containment?.ProtectionLevel,
            IsNoTakeZone = containment?.IsNoTakeZone ?? false,

            NearestMpaId = nearestMpa?.MpaId,
            NearestMpaName = nearestMpa?.MpaName,
            DistanceToNearestMpaKm = nearestMpa?.DistanceKm,

            NearestReefId = nearestReef?.Id,
            NearestReefName = nearestReef?.Name,
            DistanceToNearestReefKm = nearestReef?.DistanceKm
        };

        // Cache the result
        await _cache.SetAsync(cacheKey, context, CacheExpiry).ConfigureAwait(false);

        return context;
    }

    public async Task<IDictionary<Guid, MpaContext>> GetMpaContextBatchAsync(
        IEnumerable<(Guid Id, Point Location)> locations,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<Guid, MpaContext>();
        var locationsList = locations.ToList();

        if (!locationsList.Any())
            return results;

        // Get all MPAs once
        var allMpas = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ProtectionLevel,
                m.Boundary
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Get all reefs once
        var allReefs = await _context.Reefs
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Location,
                r.MarineProtectedAreaId
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (id, location) in locationsList)
        {
            try
            {
                // Find containing MPA
                var containingMpa = allMpas
                    .FirstOrDefault(m => m.Boundary.Contains(location));

                // Find nearest MPA using accurate distance calculation
                var nearestMpa = allMpas
                    .Select(m => new
                    {
                        m.Id,
                        m.Name,
                        m.ProtectionLevel,
                        DistanceKm = _spatialCalculator.CalculateDistanceToGeometryKm(location, m.Boundary)
                    })
                    .OrderBy(m => m.DistanceKm)
                    .FirstOrDefault();

                // Find nearest reef using accurate distance calculation
                var nearestReef = allReefs
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        DistanceKm = _spatialCalculator.CalculateDistanceKm(location, (Point)r.Location)
                    })
                    .OrderBy(r => r.DistanceKm)
                    .FirstOrDefault();

                results[id] = new MpaContext
                {
                    IsWithinMpa = containingMpa != null,
                    CurrentMpaId = containingMpa?.Id,
                    CurrentMpaName = containingMpa?.Name,
                    CurrentProtectionLevel = containingMpa?.ProtectionLevel,
                    IsNoTakeZone = containingMpa?.ProtectionLevel == ProtectionLevel.NoTake,

                    NearestMpaId = nearestMpa?.Id,
                    NearestMpaName = nearestMpa?.Name,
                    DistanceToNearestMpaKm = nearestMpa != null
                        ? (containingMpa != null ? 0 : nearestMpa.DistanceKm)
                        : null,

                    NearestReefId = nearestReef?.Id,
                    NearestReefName = nearestReef?.Name,
                    DistanceToNearestReefKm = nearestReef?.DistanceKm
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get MPA context for location {Id}", id);
                results[id] = new MpaContext();
            }
        }

        return results;
    }

    private static Point FindNearestPointOnBoundary(Point location, Geometry boundary)
    {
        // Use NTS to find the closest point on the boundary to our location
        var nearestPoints = NetTopologySuite.Operation.Distance.DistanceOp.NearestPoints(boundary, location);
        if (nearestPoints != null && nearestPoints.Length >= 1)
        {
            var coord = nearestPoints[0];
            return new Point(coord.X, coord.Y) { SRID = 4326 };
        }

        return location;
    }
}
