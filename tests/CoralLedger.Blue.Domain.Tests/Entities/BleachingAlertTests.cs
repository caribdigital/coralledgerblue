using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class BleachingAlertTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private static Point CreateTestPoint(double lat = 24.5, double lon = -77.5)
    {
        return GeometryFactory.CreatePoint(new Coordinate(lon, lat));
    }

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var location = CreateTestPoint();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var sst = 29.5;
        var sstAnomaly = 1.2;
        var dhw = 5.0;
        var hotSpot = 1.5;

        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            location,
            date,
            sst,
            sstAnomaly,
            dhw,
            hotSpot);

        // Assert
        alert.Location.Should().Be(location);
        alert.Date.Should().Be(date);
        alert.SeaSurfaceTemperature.Should().Be(sst);
        alert.SstAnomaly.Should().Be(sstAnomaly);
        alert.DegreeHeatingWeek.Should().Be(dhw);
        alert.HotSpot.Should().Be(hotSpot);
        alert.Id.Should().NotBeEmpty();
        alert.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithMpaId_SetsMarineProtectedAreaId()
    {
        // Arrange
        var mpaId = Guid.NewGuid();

        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 2.0,
            mpaId: mpaId);

        // Assert
        alert.MarineProtectedAreaId.Should().Be(mpaId);
    }

    [Fact]
    public void Create_WithReefId_SetsReefId()
    {
        // Arrange
        var reefId = Guid.NewGuid();

        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 2.0,
            reefId: reefId);

        // Assert
        alert.ReefId.Should().Be(reefId);
    }

    // ===== Alert Level Calculation Tests (Critical Business Logic) =====

    [Theory]
    [InlineData(0, null, BleachingAlertLevel.NoStress)]
    [InlineData(0, 0.0, BleachingAlertLevel.NoStress)]
    [InlineData(-0.5, null, BleachingAlertLevel.NoStress)] // Negative DHW should still be NoStress
    public void CalculateAlertLevel_WithZeroOrNegativeDhw_ReturnsNoStress(
        double dhw, double? hotSpot, BleachingAlertLevel expected)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: dhw,
            hotSpot: hotSpot);

        // Assert
        alert.AlertLevel.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(3.9)]
    public void CalculateAlertLevel_WithLowDhw_ReturnsBleachingWatch(double dhw)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.5,
            sstAnomaly: 0.8,
            dhw: dhw);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.BleachingWatch);
    }

    [Theory]
    [InlineData(4.0, null)]
    [InlineData(5.0, 0.0)]
    [InlineData(6.0, 0.5)]
    [InlineData(7.9, 0.9)]
    public void CalculateAlertLevel_WithModerateDhwNoActiveHotSpot_ReturnsBleachingWarning(
        double dhw, double? hotSpot)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 29.0,
            sstAnomaly: 1.0,
            dhw: dhw,
            hotSpot: hotSpot);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.BleachingWarning);
    }

    [Theory]
    [InlineData(4.0, 1.0)]
    [InlineData(5.0, 1.5)]
    [InlineData(6.0, 2.0)]
    [InlineData(7.9, 1.0)]
    public void CalculateAlertLevel_WithModerateDhwAndActiveHotSpot_ReturnsAlertLevel1(
        double dhw, double hotSpot)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 29.5,
            sstAnomaly: 1.5,
            dhw: dhw,
            hotSpot: hotSpot);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel1);
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(10.0)]
    [InlineData(11.9)]
    public void CalculateAlertLevel_WithHighDhw_ReturnsAlertLevel2(double dhw)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 30.0,
            sstAnomaly: 2.0,
            dhw: dhw);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel2);
    }

    [Theory]
    [InlineData(12.0)]
    [InlineData(14.0)]
    [InlineData(15.9)]
    public void CalculateAlertLevel_WithVeryHighDhw_ReturnsAlertLevel3(double dhw)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 30.5,
            sstAnomaly: 2.5,
            dhw: dhw);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel3);
    }

    [Theory]
    [InlineData(16.0)]
    [InlineData(18.0)]
    [InlineData(19.9)]
    public void CalculateAlertLevel_WithSevereDhw_ReturnsAlertLevel4(double dhw)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 31.0,
            sstAnomaly: 3.0,
            dhw: dhw);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel4);
    }

    [Theory]
    [InlineData(20.0)]
    [InlineData(25.0)]
    [InlineData(30.0)]
    public void CalculateAlertLevel_WithExtremeDhw_ReturnsAlertLevel5(double dhw)
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 32.0,
            sstAnomaly: 4.0,
            dhw: dhw);

        // Assert
        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel5);
    }

    [Fact]
    public void UpdateMetrics_UpdatesAllMetricsAndRecalculatesAlertLevel()
    {
        // Arrange
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 2.0); // BleachingWatch level

        alert.AlertLevel.Should().Be(BleachingAlertLevel.BleachingWatch);

        // Act - Update to extreme values
        alert.UpdateMetrics(
            sst: 32.0,
            sstAnomaly: 4.0,
            dhw: 20.0,
            hotSpot: 5.0);

        // Assert
        alert.SeaSurfaceTemperature.Should().Be(32.0);
        alert.SstAnomaly.Should().Be(4.0);
        alert.DegreeHeatingWeek.Should().Be(20.0);
        alert.HotSpot.Should().Be(5.0);
        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel5);
        alert.ModifiedAt.Should().NotBeNull();
        alert.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateMetrics_CanDowngradeAlertLevel()
    {
        // Arrange - Start at high alert level
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 30.0,
            sstAnomaly: 2.0,
            dhw: 10.0); // AlertLevel2

        alert.AlertLevel.Should().Be(BleachingAlertLevel.AlertLevel2);

        // Act - Conditions improve
        alert.UpdateMetrics(
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 1.0,
            hotSpot: null);

        // Assert - Alert level downgraded
        alert.AlertLevel.Should().Be(BleachingAlertLevel.BleachingWatch);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange & Act
        var alert1 = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 2.0);

        var alert2 = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 2.0);

        // Assert
        alert1.Id.Should().NotBe(alert2.Id);
    }

    [Fact]
    public void Create_WithNullHotSpot_HandlesGracefully()
    {
        // Act
        var alert = BleachingAlert.Create(Guid.NewGuid(), 
            CreateTestPoint(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            sst: 28.0,
            sstAnomaly: 0.5,
            dhw: 5.0,
            hotSpot: null);

        // Assert
        alert.HotSpot.Should().BeNull();
        // DHW >= 4 without hotspot = Warning (not Alert Level 1)
        alert.AlertLevel.Should().Be(BleachingAlertLevel.BleachingWarning);
    }
}
