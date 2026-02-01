using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class ReefTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private static Point CreateTestPoint(double lon = -77.3554, double lat = 25.0480) =>
        GeometryFactory.CreatePoint(new Coordinate(lon, lat));

    private static Polygon CreateTestPolygon(double centerLon = -77.5, double centerLat = 24.5, double size = 0.01)
    {
        var coordinates = new[]
        {
            new Coordinate(centerLon - size, centerLat - size),
            new Coordinate(centerLon + size, centerLat - size),
            new Coordinate(centerLon + size, centerLat + size),
            new Coordinate(centerLon - size, centerLat + size),
            new Coordinate(centerLon - size, centerLat - size)
        };
        return GeometryFactory.CreatePolygon(coordinates);
    }

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var name = "Andros Barrier Reef";
        var location = CreateTestPoint(-78.0, 24.7);
        var mpaId = Guid.NewGuid();

        // Act
        var reef = Reef.Create(Guid.NewGuid(), 
            name,
            location,
            healthStatus: ReefHealth.Good,
            depthMeters: 15.5,
            marineProtectedAreaId: mpaId);

        // Assert
        reef.Name.Should().Be(name);
        reef.Location.Should().Be(location);
        reef.HealthStatus.Should().Be(ReefHealth.Good);
        reef.DepthMeters.Should().Be(15.5);
        reef.MarineProtectedAreaId.Should().Be(mpaId);
        reef.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        // Arrange
        var location = CreateTestPoint();

        // Act
        var reef1 = Reef.Create(Guid.NewGuid(), "Reef 1", location);
        var reef2 = Reef.Create(Guid.NewGuid(), "Reef 2", location);

        // Assert
        reef1.Id.Should().NotBeEmpty();
        reef2.Id.Should().NotBeEmpty();
        reef1.Id.Should().NotBe(reef2.Id);
    }

    [Fact]
    public void Create_WithLocation_SetsLocation()
    {
        // Arrange
        var name = "Test Reef";
        var pointLocation = CreateTestPoint(-76.5, 24.5);
        var polygonLocation = CreateTestPolygon(-77.0, 25.0);

        // Act
        var reefWithPoint = Reef.Create(Guid.NewGuid(), name, pointLocation);
        var reefWithPolygon = Reef.Create(Guid.NewGuid(), name, polygonLocation);

        // Assert
        reefWithPoint.Location.Should().Be(pointLocation);
        reefWithPoint.Location.GeometryType.Should().Be("Point");

        reefWithPolygon.Location.Should().Be(polygonLocation);
        reefWithPolygon.Location.GeometryType.Should().Be("Polygon");
    }

    [Fact]
    public void UpdateHealth_UpdatesHealthStatus()
    {
        // Arrange
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint(), healthStatus: ReefHealth.Good);
        reef.HealthStatus.Should().Be(ReefHealth.Good);

        // Act
        reef.UpdateHealth(ReefHealth.Fair, coralCover: 35.0, bleaching: 15.0);

        // Assert
        reef.HealthStatus.Should().Be(ReefHealth.Fair);
        reef.CoralCoverPercentage.Should().Be(35.0);
        reef.BleachingPercentage.Should().Be(15.0);
    }

    [Fact]
    public void UpdateHealth_SetsModifiedAt()
    {
        // Arrange
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint());
        var originalModifiedAt = reef.ModifiedAt;

        // Act
        reef.UpdateHealth(ReefHealth.Poor, coralCover: 20.0, bleaching: 30.0);

        // Assert
        reef.ModifiedAt.Should().NotBe(originalModifiedAt);
        reef.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateHealth_SetsLastSurveyDate()
    {
        // Arrange
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint());
        reef.LastSurveyDate.Should().BeNull();

        // Act
        reef.UpdateHealth(ReefHealth.Good, coralCover: 50.0, bleaching: 5.0);

        // Assert
        reef.LastSurveyDate.Should().NotBeNull();
        reef.LastSurveyDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void AssignToMpa_SetsMpaReference()
    {
        // Arrange
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint());
        var mpaId = Guid.NewGuid();
        reef.MarineProtectedAreaId.Should().BeNull();

        // Act
        reef.AssignToMpa(mpaId);

        // Assert
        reef.MarineProtectedAreaId.Should().Be(mpaId);
        reef.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(ReefHealth.Excellent)]
    [InlineData(ReefHealth.Good)]
    [InlineData(ReefHealth.Fair)]
    [InlineData(ReefHealth.Poor)]
    [InlineData(ReefHealth.Critical)]
    [InlineData(ReefHealth.Unknown)]
    public void Create_SupportsAllHealthLevels(ReefHealth healthStatus)
    {
        // Act
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint(), healthStatus: healthStatus);

        // Assert
        reef.HealthStatus.Should().Be(healthStatus);
    }

    [Fact]
    public void Create_WithCoralCover_CanBeSetViaUpdateHealth()
    {
        // Arrange
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint());
        reef.CoralCoverPercentage.Should().BeNull();

        // Act
        reef.UpdateHealth(ReefHealth.Good, coralCover: 65.5, bleaching: 2.5);

        // Assert
        reef.CoralCoverPercentage.Should().Be(65.5);
    }

    [Fact]
    public void Create_WithDefaultHealthStatus_SetsUnknown()
    {
        // Act
        var reef = Reef.Create(Guid.NewGuid(), "Test Reef", CreateTestPoint());

        // Assert
        reef.HealthStatus.Should().Be(ReefHealth.Unknown);
    }

    [Fact]
    public void Create_WithDepthMeters_SetsDepth()
    {
        // Arrange
        var depth = 25.0;

        // Act
        var reef = Reef.Create(Guid.NewGuid(), "Deep Reef", CreateTestPoint(), depthMeters: depth);

        // Assert
        reef.DepthMeters.Should().Be(depth);
    }
}
