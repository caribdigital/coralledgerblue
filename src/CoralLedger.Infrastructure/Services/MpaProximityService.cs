using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Enums;
using CoralLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CoralLedger.Infrastructure.Services;

/// <summary>
/// Provides spatial analysis for Marine Protected Area proximity.
/// Uses PostGIS spatial functions for efficient distance and containment queries.
/// </summary>
public class MpaProximityService : IMpaProximityService
{
    private readonly MarineDbContext _context;
    private readonly ILogger<MpaProximityService> _logger;
    private readonly ICacheService _cache;

    private const string CachePrefix = "mpa_proximity_";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(15);

    // Convert degrees to approximate kilometers for distance calculations
    // At ~25°N (Bahamas latitude), 1 degree = ~111km latitude, ~100km longitude
    private const double DegreesToKmFactor = 111.0;

    public MpaProximityService(
        MarineDbContext context,
        ILogger<MpaProximityService> logger,
        ICacheService cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
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
                m.Boundary,
                Distance = m.Boundary.Distance(location)
            })
            .OrderBy(m => m.Distance)
            .FirstOrDefaultAsync(cancellationToken);

        if (mpas == null)
            return null;

        var isWithin = mpas.Distance <= 0 || mpas.Boundary.Contains(location);
        var distanceKm = mpas.Distance * DegreesToKmFactor;

        // Find the nearest point on the MPA boundary
        var nearestPoint = mpas.Boundary.Boundary is Geometry boundary
            ? FindNearestPointOnBoundary(location, boundary)
            : location;

        return new MpaProximityResult
        {
            MpaId = mpas.Id,
            MpaName = mpas.Name,
            ProtectionLevel = mpas.ProtectionLevel,
            DistanceKm = isWithin ? 0 : distanceKm,
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
                m.Boundary,
                DistanceToBoundary = m.Boundary.Boundary.Distance(location)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (containingMpa == null)
            return null;

        // Find nearest reef within the same MPA
        var nearestReef = await _context.Reefs
            .AsNoTracking()
            .Where(r => r.MarineProtectedAreaId == containingMpa.Id)
            .Select(r => new
            {
                r.Id,
                r.Name,
                Distance = r.Location.Distance(location)
            })
            .OrderBy(r => r.Distance)
            .FirstOrDefaultAsync(cancellationToken);

        return new MpaContainmentResult
        {
            MpaId = containingMpa.Id,
            MpaName = containingMpa.Name,
            ProtectionLevel = containingMpa.ProtectionLevel,
            IsNoTakeZone = containingMpa.ProtectionLevel == ProtectionLevel.NoTake,
            DistanceToNearestBoundaryKm = containingMpa.DistanceToBoundary * DegreesToKmFactor,
            NearestReefId = nearestReef?.Id,
            NearestReefName = nearestReef?.Name
        };
    }

    public async Task<IEnumerable<MpaProximityResult>> FindMpasWithinRadiusAsync(
        Point location,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        // Convert km to degrees for PostGIS query
        var radiusDegrees = radiusKm / DegreesToKmFactor;

        var mpas = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Where(m => m.Boundary.Distance(location) <= radiusDegrees)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ProtectionLevel,
                m.Boundary,
                Distance = m.Boundary.Distance(location)
            })
            .OrderBy(m => m.Distance)
            .ToListAsync(cancellationToken);

        return mpas.Select(m =>
        {
            var isWithin = m.Distance <= 0 || m.Boundary.Contains(location);
            var nearestPoint = m.Boundary.Boundary is Geometry boundary
                ? FindNearestPointOnBoundary(location, boundary)
                : location;

            return new MpaProximityResult
            {
                MpaId = m.Id,
                MpaName = m.Name,
                ProtectionLevel = m.ProtectionLevel,
                DistanceKm = isWithin ? 0 : m.Distance * DegreesToKmFactor,
                NearestBoundaryPoint = nearestPoint,
                IsWithinMpa = isWithin
            };
        });
    }

    public async Task<MpaContext> GetMpaContextAsync(
        Point location,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"{CachePrefix}{location.X:F6}_{location.Y:F6}";
        var cached = await _cache.GetAsync<MpaContext>(cacheKey);
        if (cached != null)
            return cached;

        // Check if inside an MPA
        var containment = await CheckMpaContainmentAsync(location, cancellationToken);

        // Find nearest MPA (even if inside one, to get boundary distance)
        var nearestMpa = await FindNearestMpaAsync(location, cancellationToken);

        // Find nearest reef regardless of MPA
        var nearestReef = await _context.Reefs
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                Distance = r.Location.Distance(location)
            })
            .OrderBy(r => r.Distance)
            .FirstOrDefaultAsync(cancellationToken);

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
            DistanceToNearestReefKm = nearestReef?.Distance * DegreesToKmFactor
        };

        // Cache the result
        await _cache.SetAsync(cacheKey, context, CacheExpiry);

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
            .ToListAsync(cancellationToken);

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
            .ToListAsync(cancellationToken);

        foreach (var (id, location) in locationsList)
        {
            try
            {
                // Find containing MPA
                var containingMpa = allMpas
                    .FirstOrDefault(m => m.Boundary.Contains(location));

                // Find nearest MPA
                var nearestMpa = allMpas
                    .Select(m => new
                    {
                        m.Id,
                        m.Name,
                        m.ProtectionLevel,
                        Distance = m.Boundary.Distance(location)
                    })
                    .OrderBy(m => m.Distance)
                    .FirstOrDefault();

                // Find nearest reef
                var nearestReef = allReefs
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        Distance = r.Location.Distance(location)
                    })
                    .OrderBy(r => r.Distance)
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
                        ? (containingMpa != null ? 0 : nearestMpa.Distance * DegreesToKmFactor)
                        : null,

                    NearestReefId = nearestReef?.Id,
                    NearestReefName = nearestReef?.Name,
                    DistanceToNearestReefKm = nearestReef?.Distance * DegreesToKmFactor
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
