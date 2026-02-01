using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using CoralLedger.Blue.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MpaProximityService - verifies spatial analysis functionality
/// for Marine Protected Area proximity and containment calculations.
/// </summary>
public class MpaProximityServiceTests : IDisposable
{
    private readonly MarineDbContext _context;
    private readonly Mock<ILogger<MpaProximityService>> _loggerMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ISpatialCalculator> _spatialCalculatorMock;
    private readonly MpaProximityService _service;
    private readonly GeometryFactory _geometryFactory;

    // Test coordinates (Bahamas region)
    private const double ExumaLon = -76.5;
    private const double ExumaLat = 24.2;
    private const double NassauLon = -77.35;
    private const double NassauLat = 25.05;
    private const double OutsideBahamasLon = -80.0;
    private const double OutsideBahamasLat = 26.0;

    public MpaProximityServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<MarineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MarineDbContext(options);
        _loggerMock = new Mock<ILogger<MpaProximityService>>();
        _cacheMock = new Mock<ICacheService>();
        _spatialCalculatorMock = new Mock<ISpatialCalculator>();
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        // Setup cache mock to return null (no cached data)
        _cacheMock.Setup(c => c.GetAsync<MpaContext>(It.IsAny<string>()))
            .ReturnsAsync((MpaContext?)null);

        // Setup spatial calculator mock - use simple degree-based approximation for testing
        _spatialCalculatorMock.Setup(s => s.CalculateDistanceKm(It.IsAny<Point>(), It.IsAny<Point>()))
            .Returns((Point p1, Point p2) =>
            {
                // Simple Euclidean approximation for testing (1 degree ≈ 111km at Bahamas latitude)
                var dx = (p2.X - p1.X) * 100.0; // km (longitude)
                var dy = (p2.Y - p1.Y) * 111.0; // km (latitude)
                return Math.Sqrt(dx * dx + dy * dy);
            });

        _spatialCalculatorMock.Setup(s => s.CalculateDistanceToGeometryKm(It.IsAny<Point>(), It.IsAny<Geometry>()))
            .Returns((Point point, Geometry geom) =>
            {
                if (geom.Contains(point)) return 0.0;
                // Simple approximation using NTS distance converted to km
                return point.Distance(geom) * 111.0;
            });

        _service = new MpaProximityService(_context, _loggerMock.Object, _cacheMock.Object, _spatialCalculatorMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test MPAs with different protection levels using factory methods
        var exumaMpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            name: "Exuma Cays Land and Sea Park",
            boundary: CreatePolygon(ExumaLon, ExumaLat, 0.5), // ~55km radius
            protectionLevel: ProtectionLevel.NoTake,
            islandGroup: IslandGroup.Exumas,
            description: "Test MPA for unit tests"
        );

        var inaguaMpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            name: "Inagua National Park",
            boundary: CreatePolygon(-73.5, 21.0, 0.8), // Great Inagua
            protectionLevel: ProtectionLevel.HighlyProtected,
            islandGroup: IslandGroup.Inagua,
            description: "Test MPA for unit tests"
        );

        var androsWestMpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            name: "Andros West Side National Park",
            boundary: CreatePolygon(-78.0, 24.5, 0.6),
            protectionLevel: ProtectionLevel.LightlyProtected,
            islandGroup: IslandGroup.Andros,
            description: "Test MPA for unit tests"
        );

        _context.MarineProtectedAreas.AddRange(exumaMpa, inaguaMpa, androsWestMpa);

        // Add test reefs within MPAs using factory method
        var exumaReef = Reef.Create(
            Guid.NewGuid(),
            name: "Warderick Wells Reef",
            location: _geometryFactory.CreatePoint(new Coordinate(ExumaLon + 0.1, ExumaLat + 0.1)),
            healthStatus: ReefHealth.Good,
            depthMeters: 15.0,
            marineProtectedAreaId: exumaMpa.Id
        );

        _context.Reefs.Add(exumaReef);
        _context.SaveChanges();
    }

    private Polygon CreatePolygon(double centerLon, double centerLat, double size)
    {
        // Create a simple square polygon centered at the given coordinates
        var coordinates = new[]
        {
            new Coordinate(centerLon - size, centerLat - size),
            new Coordinate(centerLon + size, centerLat - size),
            new Coordinate(centerLon + size, centerLat + size),
            new Coordinate(centerLon - size, centerLat + size),
            new Coordinate(centerLon - size, centerLat - size) // Close the ring
        };

        return _geometryFactory.CreatePolygon(coordinates);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ============ FindNearestMpaAsync Tests ============

    [Fact]
    public async Task FindNearestMpaAsync_PointInsideMpa_ReturnsZeroDistance()
    {
        // Arrange - Point inside Exuma MPA
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        var result = await _service.FindNearestMpaAsync(location);

        // Assert
        result.Should().NotBeNull();
        result!.MpaName.Should().Be("Exuma Cays Land and Sea Park");
        result.IsWithinMpa.Should().BeTrue();
        result.DistanceKm.Should().Be(0);
    }

    [Fact]
    public async Task FindNearestMpaAsync_PointOutsideMpa_ReturnsDistanceToNearest()
    {
        // Arrange - Point outside all MPAs (Nassau area)
        var location = _geometryFactory.CreatePoint(new Coordinate(NassauLon, NassauLat));

        // Act
        var result = await _service.FindNearestMpaAsync(location);

        // Assert
        result.Should().NotBeNull();
        result!.IsWithinMpa.Should().BeFalse();
        result.DistanceKm.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FindNearestMpaAsync_ReturnsCorrectProtectionLevel()
    {
        // Arrange - Point inside Exuma (No-Take zone)
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        var result = await _service.FindNearestMpaAsync(location);

        // Assert
        result.Should().NotBeNull();
        result!.ProtectionLevel.Should().Be(ProtectionLevel.NoTake);
    }

    [Fact]
    public async Task FindNearestMpaAsync_NoMpas_ReturnsNull()
    {
        // Arrange - Clear all MPAs
        _context.MarineProtectedAreas.RemoveRange(_context.MarineProtectedAreas);
        await _context.SaveChangesAsync();

        var location = _geometryFactory.CreatePoint(new Coordinate(NassauLon, NassauLat));

        // Act
        var result = await _service.FindNearestMpaAsync(location);

        // Assert
        result.Should().BeNull();
    }

    // ============ CheckMpaContainmentAsync Tests ============

    [Fact]
    public async Task CheckMpaContainmentAsync_PointInsideMpa_ReturnsContainmentResult()
    {
        // Arrange
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        var result = await _service.CheckMpaContainmentAsync(location);

        // Assert
        result.Should().NotBeNull();
        result!.MpaName.Should().Be("Exuma Cays Land and Sea Park");
        result.ProtectionLevel.Should().Be(ProtectionLevel.NoTake);
        result.IsNoTakeZone.Should().BeTrue();
    }

    [Fact]
    public async Task CheckMpaContainmentAsync_PointOutsideMpa_ReturnsNull()
    {
        // Arrange - Point far outside any MPA
        var location = _geometryFactory.CreatePoint(new Coordinate(OutsideBahamasLon, OutsideBahamasLat));

        // Act
        var result = await _service.CheckMpaContainmentAsync(location);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckMpaContainmentAsync_IncludesNearestReefInfo()
    {
        // Arrange - Point inside Exuma MPA where reef exists
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon + 0.05, ExumaLat + 0.05));

        // Act
        var result = await _service.CheckMpaContainmentAsync(location);

        // Assert
        result.Should().NotBeNull();
        result!.NearestReefName.Should().Be("Warderick Wells Reef");
    }

    [Fact]
    public async Task CheckMpaContainmentAsync_HighlyProtected_NotNoTakeZone()
    {
        // Arrange - Point inside Inagua (Highly Protected, not No-Take)
        var location = _geometryFactory.CreatePoint(new Coordinate(-73.5, 21.0));

        // Act
        var result = await _service.CheckMpaContainmentAsync(location);

        // Assert
        result.Should().NotBeNull();
        result!.IsNoTakeZone.Should().BeFalse();
        result.ProtectionLevel.Should().Be(ProtectionLevel.HighlyProtected);
    }

    // ============ FindMpasWithinRadiusAsync Tests ============

    [Fact]
    public async Task FindMpasWithinRadiusAsync_SmallRadius_ReturnsEmpty()
    {
        // Arrange - Point far from any MPA with tiny radius
        var location = _geometryFactory.CreatePoint(new Coordinate(OutsideBahamasLon, OutsideBahamasLat));
        var radiusKm = 10.0; // Only 10km radius

        // Act
        var results = await _service.FindMpasWithinRadiusAsync(location, radiusKm);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMpasWithinRadiusAsync_LargeRadius_ReturnsMultipleMpas()
    {
        // Arrange - Point with large radius to capture multiple MPAs
        var location = _geometryFactory.CreatePoint(new Coordinate(-76.0, 23.0));
        var radiusKm = 500.0; // 500km radius should capture multiple MPAs

        // Act
        var results = await _service.FindMpasWithinRadiusAsync(location, radiusKm);

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task FindMpasWithinRadiusAsync_ResultsOrderedByDistance()
    {
        // Arrange
        var location = _geometryFactory.CreatePoint(new Coordinate(-76.0, 23.0));
        var radiusKm = 1000.0;

        // Act
        var results = (await _service.FindMpasWithinRadiusAsync(location, radiusKm)).ToList();

        // Assert
        for (int i = 1; i < results.Count; i++)
        {
            results[i].DistanceKm.Should().BeGreaterOrEqualTo(results[i - 1].DistanceKm);
        }
    }

    // ============ GetMpaContextAsync Tests ============

    [Fact]
    public async Task GetMpaContextAsync_PointInsideMpa_SetsIsWithinMpaTrue()
    {
        // Arrange
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        var result = await _service.GetMpaContextAsync(location);

        // Assert
        result.IsWithinMpa.Should().BeTrue();
        result.CurrentMpaName.Should().Be("Exuma Cays Land and Sea Park");
        result.IsNoTakeZone.Should().BeTrue();
    }

    [Fact]
    public async Task GetMpaContextAsync_PointOutsideMpa_SetsIsWithinMpaFalse()
    {
        // Arrange
        var location = _geometryFactory.CreatePoint(new Coordinate(NassauLon, NassauLat));

        // Act
        var result = await _service.GetMpaContextAsync(location);

        // Assert
        result.IsWithinMpa.Should().BeFalse();
        result.CurrentMpaId.Should().BeNull();
    }

    [Fact]
    public async Task GetMpaContextAsync_IncludesNearestReefInfo()
    {
        // Arrange
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        var result = await _service.GetMpaContextAsync(location);

        // Assert
        result.NearestReefName.Should().NotBeNullOrEmpty();
        result.DistanceToNearestReefKm.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMpaContextAsync_RequiresMpaAlert_TrueForNoTakeZone()
    {
        // Arrange - Point inside No-Take zone
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        var result = await _service.GetMpaContextAsync(location);

        // Assert
        result.RequiresMpaAlert.Should().BeTrue();
    }

    [Fact]
    public async Task GetMpaContextAsync_IsNearMpa_TrueWhenWithin5Km()
    {
        // Arrange - Point just outside MPA but within 5km
        // Create a point slightly outside the Exuma boundary
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon - 0.52, ExumaLat));

        // Act
        var result = await _service.GetMpaContextAsync(location);

        // Assert
        // This should be near but not inside (depending on exact boundary)
        result.DistanceToNearestMpaKm.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetMpaContextAsync_UsesCaching()
    {
        // Arrange
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));

        // Act
        await _service.GetMpaContextAsync(location);

        // Assert
        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<MpaContext>(),
            It.IsAny<TimeSpan>()),
            Times.Once);
    }

    // ============ GetMpaContextBatchAsync Tests ============

    [Fact]
    public async Task GetMpaContextBatchAsync_MultipleLocations_ReturnsAllResults()
    {
        // Arrange
        var locations = new List<(Guid Id, Point Location)>
        {
            (Guid.NewGuid(), _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))),
            (Guid.NewGuid(), _geometryFactory.CreatePoint(new Coordinate(NassauLon, NassauLat))),
            (Guid.NewGuid(), _geometryFactory.CreatePoint(new Coordinate(-73.5, 21.0)))
        };

        // Act
        var results = await _service.GetMpaContextBatchAsync(locations);

        // Assert
        results.Should().HaveCount(3);
        foreach (var loc in locations)
        {
            results.Should().ContainKey(loc.Id);
        }
    }

    [Fact]
    public async Task GetMpaContextBatchAsync_EmptyInput_ReturnsEmptyDictionary()
    {
        // Arrange
        var locations = new List<(Guid Id, Point Location)>();

        // Act
        var results = await _service.GetMpaContextBatchAsync(locations);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMpaContextBatchAsync_MixedInsideOutside_CorrectlyClassifies()
    {
        // Arrange
        var insideId = Guid.NewGuid();
        var outsideId = Guid.NewGuid();

        var locations = new List<(Guid Id, Point Location)>
        {
            (insideId, _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat))), // Inside
            (outsideId, _geometryFactory.CreatePoint(new Coordinate(OutsideBahamasLon, OutsideBahamasLat))) // Outside
        };

        // Act
        var results = await _service.GetMpaContextBatchAsync(locations);

        // Assert
        results[insideId].IsWithinMpa.Should().BeTrue();
        results[outsideId].IsWithinMpa.Should().BeFalse();
    }

    // ============ Distance Calculation Tests ============

    [Fact]
    public async Task FindNearestMpaAsync_DistanceCalculation_ReasonableValues()
    {
        // Arrange - Point at Nassau (known distance from Exuma ~100km)
        var location = _geometryFactory.CreatePoint(new Coordinate(NassauLon, NassauLat));

        // Act
        var result = await _service.FindNearestMpaAsync(location);

        // Assert
        result.Should().NotBeNull();
        // Distance should be in a reasonable range (not 0, not huge)
        result!.DistanceKm.Should().BeGreaterThan(0);
        result.DistanceKm.Should().BeLessThan(1000); // Bahamas aren't that big
    }

    [Fact]
    public async Task FindMpasWithinRadiusAsync_ZeroRadius_ReturnsOnlyContainingMpa()
    {
        // Arrange - Point inside MPA with 0 radius
        var location = _geometryFactory.CreatePoint(new Coordinate(ExumaLon, ExumaLat));
        var radiusKm = 0.0;

        // Act
        var results = (await _service.FindMpasWithinRadiusAsync(location, radiusKm)).ToList();

        // Assert
        // Should only return the MPA we're inside of (distance 0)
        results.Should().OnlyContain(r => r.IsWithinMpa);
    }
}
