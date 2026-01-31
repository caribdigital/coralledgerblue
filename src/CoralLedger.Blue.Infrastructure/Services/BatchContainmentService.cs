using System.Collections.Concurrent;
using System.Diagnostics;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Optimized batch containment service for point-in-polygon queries.
/// Implements Sprint 3.3 US-3.3.2: &lt;100ms for 10K positions.
///
/// Optimizations:
/// 1. PreparedGeometry caching for O(log n) containment checks instead of O(n)
/// 2. Bounding box envelope pre-filtering before detailed containment
/// 3. Parallel processing for large batches
/// 4. In-memory MPA boundary cache to avoid repeated DB queries
/// </summary>
public class BatchContainmentService : IBatchContainmentService
{
    private readonly MarineDbContext _context;
    private readonly ILogger<BatchContainmentService> _logger;

    // Cache of prepared geometries keyed by MPA ID
    // PreparedGeometry is thread-safe for reading and provides O(log n) containment checks
    private readonly ConcurrentDictionary<Guid, PreparedMpaData> _preparedMpaCache = new();
    private volatile bool _cacheInitialized = false;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    // Performance thresholds
    private const int ParallelThreshold = 1000; // Use parallel processing above this count
    private const int BatchSize = 500; // Process points in batches for DB queries

    public BatchContainmentService(
        MarineDbContext context,
        ILogger<BatchContainmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IDictionary<int, MpaContainmentInfo?>> CheckContainmentBatchAsync(
        IReadOnlyList<Point> points,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new ConcurrentDictionary<int, MpaContainmentInfo?>();

        if (points.Count == 0)
            return results;

        // Ensure cache is warm
        await EnsureCacheWarmAsync(cancellationToken).ConfigureAwait(false);

        var mpas = _preparedMpaCache.Values.ToList();
        if (mpas.Count == 0)
        {
            // No MPAs cached, return all nulls
            for (int i = 0; i < points.Count; i++)
                results[i] = null;
            return results;
        }

        // Choose processing strategy based on point count
        if (points.Count >= ParallelThreshold)
        {
            // Parallel processing for large batches
            await ProcessPointsParallelAsync(points, mpas, results, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Sequential processing for smaller batches
            ProcessPointsSequential(points, mpas, results);
        }

        sw.Stop();
        _logger.LogDebug(
            "Batch containment check completed: {Count} points in {Elapsed}ms ({PerPoint:F3}ms/point)",
            points.Count, sw.ElapsedMilliseconds, (double)sw.ElapsedMilliseconds / points.Count);

        // Performance warning if we exceed target
        if (sw.ElapsedMilliseconds > 100 && points.Count >= 10000)
        {
            _logger.LogWarning(
                "Batch containment exceeded 100ms target: {Count} points took {Elapsed}ms",
                points.Count, sw.ElapsedMilliseconds);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> CheckContainmentInMpaAsync(
        IReadOnlyList<Point> points,
        Guid mpaId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCacheWarmAsync(cancellationToken).ConfigureAwait(false);

        if (!_preparedMpaCache.TryGetValue(mpaId, out var mpaData))
        {
            _logger.LogWarning("MPA {MpaId} not found in cache", mpaId);
            return Array.Empty<int>();
        }

        var results = new List<int>();

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];

            // Quick envelope check first using coordinate
            if (!mpaData.Envelope.Contains(point.Coordinate))
                continue;

            // Detailed containment check
            if (mpaData.PreparedGeometry.Contains(point))
            {
                results.Add(i);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> FindMpasInBoundingBoxAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        CancellationToken cancellationToken = default)
    {
        // Create bounding box envelope
        var envelope = new Envelope(minLon, maxLon, minLat, maxLat);

        // Check cache first for in-memory filtering (faster for cached data)
        if (_cacheInitialized)
        {
            return _preparedMpaCache
                .Where(kvp => kvp.Value.Envelope.Intersects(envelope))
                .Select(kvp => kvp.Key)
                .ToList();
        }

        // Fall back to database query with GiST index
        var bbox = new GeometryFactory(new PrecisionModel(), 4326)
            .ToGeometry(envelope);

        var mpaIds = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Where(m => m.Boundary.Intersects(bbox))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return mpaIds;
    }

    /// <inheritdoc />
    public async Task WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cacheInitialized)
                return;

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Warming MPA containment cache...");

            // Load all MPA boundaries from database
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

            // Create prepared geometries for each MPA
            foreach (var mpa in mpas)
            {
                try
                {
                    var preparedGeometry = PreparedGeometryFactory.Prepare(mpa.Boundary);
                    var envelope = mpa.Boundary.EnvelopeInternal;

                    _preparedMpaCache[mpa.Id] = new PreparedMpaData
                    {
                        MpaId = mpa.Id,
                        MpaName = mpa.Name,
                        ProtectionLevel = mpa.ProtectionLevel.ToString(),
                        IsNoTakeZone = mpa.ProtectionLevel == ProtectionLevel.NoTake,
                        PreparedGeometry = preparedGeometry,
                        Envelope = envelope
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to prepare geometry for MPA {MpaId}", mpa.Id);
                }
            }

            _cacheInitialized = true;
            sw.Stop();

            _logger.LogInformation(
                "MPA containment cache warmed: {Count} MPAs loaded in {Elapsed}ms",
                _preparedMpaCache.Count, sw.ElapsedMilliseconds);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _preparedMpaCache.Clear();
        _cacheInitialized = false;
        _logger.LogInformation("MPA containment cache cleared");
    }

    private async Task EnsureCacheWarmAsync(CancellationToken cancellationToken)
    {
        if (!_cacheInitialized)
        {
            await WarmCacheAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ProcessPointsSequential(
        IReadOnlyList<Point> points,
        List<PreparedMpaData> mpas,
        ConcurrentDictionary<int, MpaContainmentInfo?> results)
    {
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            MpaContainmentInfo? containment = null;

            // Check each MPA (envelope first, then detailed)
            foreach (var mpa in mpas)
            {
                // Quick bounding box check (O(1))
                if (!mpa.Envelope.Contains(point.Coordinate))
                    continue;

                // Detailed containment check using PreparedGeometry (O(log n))
                if (mpa.PreparedGeometry.Contains(point))
                {
                    containment = new MpaContainmentInfo
                    {
                        MpaId = mpa.MpaId,
                        MpaName = mpa.MpaName,
                        ProtectionLevel = mpa.ProtectionLevel,
                        IsNoTakeZone = mpa.IsNoTakeZone
                    };
                    break; // Found containing MPA, stop checking
                }
            }

            results[i] = containment;
        }
    }

    private Task ProcessPointsParallelAsync(
        IReadOnlyList<Point> points,
        List<PreparedMpaData> mpas,
        ConcurrentDictionary<int, MpaContainmentInfo?> results,
        CancellationToken cancellationToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        Parallel.For(0, points.Count, options, i =>
        {
            var point = points[i];
            MpaContainmentInfo? containment = null;

            foreach (var mpa in mpas)
            {
                // Quick bounding box check
                if (!mpa.Envelope.Contains(point.Coordinate))
                    continue;

                // Detailed containment check
                if (mpa.PreparedGeometry.Contains(point))
                {
                    containment = new MpaContainmentInfo
                    {
                        MpaId = mpa.MpaId,
                        MpaName = mpa.MpaName,
                        ProtectionLevel = mpa.ProtectionLevel,
                        IsNoTakeZone = mpa.IsNoTakeZone
                    };
                    break;
                }
            }

            results[i] = containment;
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cached data for a single MPA with prepared geometry.
    /// </summary>
    private class PreparedMpaData
    {
        public required Guid MpaId { get; init; }
        public required string MpaName { get; init; }
        public required string ProtectionLevel { get; init; }
        public bool IsNoTakeZone { get; init; }

        /// <summary>
        /// PreparedGeometry for O(log n) containment checks.
        /// Thread-safe for concurrent read access.
        /// </summary>
        public required IPreparedGeometry PreparedGeometry { get; init; }

        /// <summary>
        /// Bounding box for quick O(1) rejection of points outside envelope.
        /// </summary>
        public required Envelope Envelope { get; init; }
    }
}
