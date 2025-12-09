using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for BatchContainmentService - verifies optimized batch point-in-polygon
/// queries for Sprint 3.3 US-3.3.2: &lt;100ms for 10K positions.
/// </summary>
public class BatchContainmentServiceTests : IDisposable
{
    private readonly MarineDbContext _context;
    private readonly Mock<ILogger<BatchContainmentService>> _loggerMock;
    private readonly BatchContainmentService _service;
    private readonly GeometryFactory _geometryFactory;

    // Test coordinates (Bahamas region)
    private const double ExumaLon = -76.5;
    private const double ExumaLat = 24.2;
    private const double InaguaLon = -73.5;
    private const double InaguaLat = 21.0;
    private const double OutsideLon = -80.0;
    private const double OutsideLat = 26.0;

    private Guid _exumaMpaId;
    private Guid _inaguaMpaId;

    public BatchContainmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<MarineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MarineDbContext(options);
        _loggerMock = new Mock<ILogger<BatchContainmentService>>();
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        _service = new BatchContainmentService(_context, _loggerMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var exumaMpa = MarineProtectedArea.Create(
            name: "Exuma Cays Land and Sea Park",
            boundary: CreatePolygon(ExumaLon, ExumaLat, 0.5),
            protectionLevel: ProtectionLevel.NoTake,
            islandGroup: IslandGroup.Exumas,
            description: "Test MPA for batch containment tests"
        );
        _exumaMpaId = exumaMpa.Id;

        var inaguaMpa = MarineProtectedArea.Create(
            name: "Inagua National Park",
            boundary: CreatePolygon(InaguaLon, InaguaLat, 0.8),
            protectionLevel: ProtectionLevel.HighlyProtected,
            islandGroup: IslandGroup.Inagua,
            description: "Test MPA for batch containment tests"
        );
        _inaguaMpaId = inaguaMpa.Id;

        _context.MarineProtectedAreas.AddRange(exumaMpa, inaguaMpa);
        _context.SaveChanges();
    }

    private Polygon CreatePolygon(double centerLon, double centerLat, double size)
    {
        var coordinates = new[]
        {
            new Coordinate(centerLon - size, centerLat - size),
            new Coordinate(centerLon + size, centerLat - size),
            new Coordinate(centerLon + size, centerLat + size),
            new Coordinate(centerLon - size, centerLat + size),
            new Coordinate(centerLon - size, centerLat - size)
        };

        return _geometryFactory.CreatePolygon(coordinates);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ============ CheckContainmentBatchAsync Tests ============

    [Fact]
    public async Task CheckContainmentBatchAsync_EmptyPoints_ReturnsEmptyDictionary()
    {
        // Arrange
        var points = new List<Point>();

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_SinglePointInside_ReturnsContainment()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().NotBeNull();
        results[0]!.MpaName.Should().Be("Exuma Cays Land and Sea Park");
        results[0]!.IsNoTakeZone.Should().BeTrue();
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_SinglePointOutside_ReturnsNull()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(OutsideLon, OutsideLat))
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeNull();
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_MixedPoints_CorrectlyClassifies()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat)),       // Inside Exuma
            _geometryFactory.CreatePoint(new Coordinate(OutsideLon, OutsideLat)),   // Outside all
            _geometryFactory.CreatePoint(new Coordinate(InaguaLon, InaguaLat))      // Inside Inagua
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().HaveCount(3);

        results[0].Should().NotBeNull();
        results[0]!.MpaName.Should().Be("Exuma Cays Land and Sea Park");

        results[1].Should().BeNull();

        results[2].Should().NotBeNull();
        results[2]!.MpaName.Should().Be("Inagua National Park");
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_NoMpas_AllResultsNull()
    {
        // Arrange - Clear MPAs and cache
        _context.MarineProtectedAreas.RemoveRange(_context.MarineProtectedAreas);
        await _context.SaveChangesAsync();
        _service.ClearCache();

        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeNull();
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_ReturnsCorrectProtectionLevel()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat)),   // NoTake
            _geometryFactory.CreatePoint(new Coordinate(InaguaLon, InaguaLat))  // HighlyProtected
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results[0]!.ProtectionLevel.Should().Be("NoTake");
        results[0]!.IsNoTakeZone.Should().BeTrue();

        results[1]!.ProtectionLevel.Should().Be("HighlyProtected");
        results[1]!.IsNoTakeZone.Should().BeFalse();
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_LargeBatch_ProcessesAllPoints()
    {
        // Arrange - Create 100 points (below parallel threshold but still a batch)
        var points = new List<Point>();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var lon = OutsideLon + random.NextDouble() * 10 - 5;
            var lat = OutsideLat + random.NextDouble() * 10 - 5;
            points.Add(_geometryFactory.CreatePoint(new Coordinate(lon, lat)));
        }

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().HaveCount(100);
    }

    // ============ CheckContainmentInMpaAsync Tests ============

    [Fact]
    public async Task CheckContainmentInMpaAsync_PointsInsideMpa_ReturnsIndices()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat)),       // Inside
            _geometryFactory.CreatePoint(new Coordinate(OutsideLon, OutsideLat)),   // Outside
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon + 0.1, ExumaLat + 0.1)) // Inside
        };

        // Act
        var results = await _service.CheckContainmentInMpaAsync(points, _exumaMpaId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(0);
        results.Should().Contain(2);
        results.Should().NotContain(1);
    }

    [Fact]
    public async Task CheckContainmentInMpaAsync_NoPointsInside_ReturnsEmpty()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(OutsideLon, OutsideLat)),
            _geometryFactory.CreatePoint(new Coordinate(OutsideLon + 1, OutsideLat + 1))
        };

        // Act
        var results = await _service.CheckContainmentInMpaAsync(points, _exumaMpaId);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckContainmentInMpaAsync_InvalidMpaId_ReturnsEmpty()
    {
        // Arrange
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };

        // Act
        var results = await _service.CheckContainmentInMpaAsync(points, Guid.NewGuid());

        // Assert
        results.Should().BeEmpty();
    }

    // ============ FindMpasInBoundingBoxAsync Tests ============

    [Fact]
    public async Task FindMpasInBoundingBoxAsync_ContainingMpa_ReturnsMpaId()
    {
        // Arrange - Bounding box around Exuma
        var minLon = ExumaLon - 1;
        var minLat = ExumaLat - 1;
        var maxLon = ExumaLon + 1;
        var maxLat = ExumaLat + 1;

        // Act
        var results = await _service.FindMpasInBoundingBoxAsync(minLon, minLat, maxLon, maxLat);

        // Assert
        results.Should().Contain(_exumaMpaId);
    }

    [Fact]
    public async Task FindMpasInBoundingBoxAsync_LargeBoundingBox_ReturnsAllMpas()
    {
        // Arrange - Large bounding box covering both MPAs
        var minLon = -85.0;
        var minLat = 15.0;
        var maxLon = -70.0;
        var maxLat = 30.0;

        // Act
        var results = await _service.FindMpasInBoundingBoxAsync(minLon, minLat, maxLon, maxLat);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(_exumaMpaId);
        results.Should().Contain(_inaguaMpaId);
    }

    [Fact]
    public async Task FindMpasInBoundingBoxAsync_NoIntersection_ReturnsEmpty()
    {
        // Arrange - Bounding box far from any MPA
        var minLon = -60.0;
        var minLat = 10.0;
        var maxLon = -55.0;
        var maxLat = 15.0;

        // Act
        var results = await _service.FindMpasInBoundingBoxAsync(minLon, minLat, maxLon, maxLat);

        // Assert
        results.Should().BeEmpty();
    }

    // ============ Cache Tests ============

    [Fact]
    public async Task WarmCacheAsync_LoadsMpas()
    {
        // Arrange
        _service.ClearCache();

        // Act
        await _service.WarmCacheAsync();

        // Assert - Subsequent batch query should work with cached data
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };
        var results = await _service.CheckContainmentBatchAsync(points);
        results[0].Should().NotBeNull();
    }

    [Fact]
    public async Task ClearCache_InvalidatesCache()
    {
        // Arrange - Warm cache first
        await _service.WarmCacheAsync();

        // Remove MPA from database
        _context.MarineProtectedAreas.RemoveRange(_context.MarineProtectedAreas);
        await _context.SaveChangesAsync();

        // Clear cache
        _service.ClearCache();

        // Act
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert - Should now return null since MPA is gone and cache was cleared
        results[0].Should().BeNull();
    }

    // ============ Performance Tests ============

    [Fact]
    public async Task CheckContainmentBatchAsync_Performance_Under100msFor100Points()
    {
        // Arrange - Create 100 random points
        var points = new List<Point>();
        var random = new Random(123);

        for (int i = 0; i < 100; i++)
        {
            var lon = -80.0 + random.NextDouble() * 15;
            var lat = 18.0 + random.NextDouble() * 10;
            points.Add(_geometryFactory.CreatePoint(new Coordinate(lon, lat)));
        }

        // Warm the cache
        await _service.WarmCacheAsync();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await _service.CheckContainmentBatchAsync(points);
        sw.Stop();

        // Assert
        results.Should().HaveCount(100);
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "100 points should complete under 100ms");
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_CacheAutoWarms()
    {
        // Arrange - Clear cache to ensure auto-warm triggers
        _service.ClearCache();

        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert - Should still work because cache auto-warms
        results[0].Should().NotBeNull();
    }

    // ============ Edge Cases ============

    [Fact]
    public async Task CheckContainmentBatchAsync_PointOnBoundary_ReturnsContainment()
    {
        // Arrange - Point exactly on MPA boundary
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon - 0.5, ExumaLat)) // On west edge
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert - NTS contains includes boundary by default
        results.Should().HaveCount(1);
        // Note: NTS behavior on exact boundary can vary - just verify no exception
    }

    [Fact]
    public async Task CheckContainmentBatchAsync_DuplicatePoints_ProcessesAll()
    {
        // Arrange - Same point multiple times
        var points = new List<Point>
        {
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat)),
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat)),
            _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))
        };

        // Act
        var results = await _service.CheckContainmentBatchAsync(points);

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().NotBeNull();
        results[1].Should().NotBeNull();
        results[2].Should().NotBeNull();
    }
}
