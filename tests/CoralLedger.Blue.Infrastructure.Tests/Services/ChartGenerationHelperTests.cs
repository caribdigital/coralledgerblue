using CoralLedger.Blue.Application.Features.Reports.DTOs;
using CoralLedger.Blue.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ChartGenerationHelper - verifies chart and map generation for PDF reports
/// </summary>
public class ChartGenerationHelperTests
{
    [Fact]
    public void GenerateBleachingTrendChart_WithValidData_ReturnsImageBytes()
    {
        // Arrange
        var alerts = new List<BleachingAlertItem>
        {
            new() { Date = DateTime.UtcNow.AddDays(-10), DegreeHeatingWeeks = 2.5, SeaSurfaceTemp = 28.5, AlertLevel = "Watch" },
            new() { Date = DateTime.UtcNow.AddDays(-5), DegreeHeatingWeeks = 3.8, SeaSurfaceTemp = 29.2, AlertLevel = "Warning" },
            new() { Date = DateTime.UtcNow.AddDays(-2), DegreeHeatingWeeks = 4.5, SeaSurfaceTemp = 30.1, AlertLevel = "Critical" }
        };

        // Act
        var result = ChartGenerationHelper.GenerateBleachingTrendChart(alerts);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000); // Reasonable PNG size
    }

    [Fact]
    public void GenerateBleachingTrendChart_WithEmptyData_ReturnsEmptyArray()
    {
        // Arrange
        var alerts = new List<BleachingAlertItem>();

        // Act
        var result = ChartGenerationHelper.GenerateBleachingTrendChart(alerts);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateBleachingTrendChart_WithNullData_ReturnsEmptyArray()
    {
        // Act
        var result = ChartGenerationHelper.GenerateBleachingTrendChart(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateVesselActivityChart_WithValidData_ReturnsImageBytes()
    {
        // Arrange
        var eventsByType = new Dictionary<string, int>
        {
            { "Fishing", 15 },
            { "Port", 8 },
            { "Encounter", 3 }
        };

        // Act
        var result = ChartGenerationHelper.GenerateVesselActivityChart(eventsByType);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void GenerateVesselActivityChart_WithEmptyData_ReturnsEmptyArray()
    {
        // Arrange
        var eventsByType = new Dictionary<string, int>();

        // Act
        var result = ChartGenerationHelper.GenerateVesselActivityChart(eventsByType);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateVesselActivityChart_WithNullData_ReturnsEmptyArray()
    {
        // Act
        var result = ChartGenerationHelper.GenerateVesselActivityChart(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateObservationsStatusChart_WithValidData_ReturnsImageBytes()
    {
        // Arrange
        int approved = 25;
        int pending = 10;
        int rejected = 5;

        // Act
        var result = ChartGenerationHelper.GenerateObservationsStatusChart(approved, pending, rejected);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void GenerateObservationsStatusChart_WithAllZeros_ReturnsEmptyArray()
    {
        // Act
        var result = ChartGenerationHelper.GenerateObservationsStatusChart(0, 0, 0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateObservationsStatusChart_WithPartialData_ReturnsImageBytes()
    {
        // Arrange - only approved observations
        int approved = 25;
        int pending = 0;
        int rejected = 0;

        // Act
        var result = ChartGenerationHelper.GenerateObservationsStatusChart(approved, pending, rejected);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void GenerateMpaMap_WithValidCoordinates_ReturnsImageBytes()
    {
        // Arrange
        double lat = 24.2;
        double lon = -76.5;
        string name = "Test MPA";

        // Act
        var result = ChartGenerationHelper.GenerateMpaMap(lat, lon, name);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000);
    }

    [Theory]
    [InlineData(600, 400)]
    [InlineData(800, 600)]
    [InlineData(400, 300)]
    public void GenerateMpaMap_WithCustomSize_ReturnsImageBytes(int width, int height)
    {
        // Arrange
        double lat = 24.2;
        double lon = -76.5;
        string name = "Test MPA";

        // Act
        var result = ChartGenerationHelper.GenerateMpaMap(lat, lon, name, width, height);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void GeneratedCharts_ShouldBePngFormat()
    {
        // Arrange
        var alerts = new List<BleachingAlertItem>
        {
            new() { Date = DateTime.UtcNow, DegreeHeatingWeeks = 2.5, SeaSurfaceTemp = 28.5, AlertLevel = "Watch" }
        };

        // Act
        var result = ChartGenerationHelper.GenerateBleachingTrendChart(alerts);

        // Assert - PNG signature is 89 50 4E 47 (first 4 bytes)
        result.Should().NotBeEmpty();
        result[0].Should().Be(0x89);
        result[1].Should().Be(0x50);
        result[2].Should().Be(0x4E);
        result[3].Should().Be(0x47);
    }
}
