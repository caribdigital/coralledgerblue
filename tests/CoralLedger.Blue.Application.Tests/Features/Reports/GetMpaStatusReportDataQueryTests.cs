using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Reports.Queries;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Application.Tests.Features.Reports;

public class GetMpaStatusReportDataQueryTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private static Point CreateTestPoint(double lon = -77.5, double lat = 24.5)
    {
        return GeometryFactory.CreatePoint(new Coordinate(lon, lat));
    }

    private static Polygon CreateTestPolygon(double centerLon = -77.5, double centerLat = 24.5, double size = 0.1)
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

    private static MarineDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MarineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new MarineDbContext(options);
    }

    [Fact]
    public async Task Handle_WithValidMpaId_ReturnsReportData()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa = MarineProtectedArea.Create(
            "Test MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        context.MarineProtectedAreas.Add(mpa);
        await context.SaveChangesAsync();

        var handler = new GetMpaStatusReportDataQueryHandler(context);
        var query = new GetMpaStatusReportDataQuery(mpa.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.MpaId.Should().Be(mpa.Id);
        result.Name.Should().Be("Test MPA");
        result.ProtectionLevel.Should().Be("NoTake");
        result.IslandGroup.Should().Be("Exumas");
        result.BleachingData.Should().NotBeNull();
        result.BleachingData.TotalAlerts.Should().Be(0);
        result.FishingActivity.Should().NotBeNull();
        result.FishingActivity.TotalVesselEvents.Should().Be(0);
        result.Observations.Should().NotBeNull();
        result.Observations.TotalObservations.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNonExistentMpaId_ReturnsNull()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var nonExistentId = Guid.NewGuid();

        var handler = new GetMpaStatusReportDataQueryHandler(context);
        var query = new GetMpaStatusReportDataQuery(nonExistentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithBleachingData_IncludesInReport()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa = MarineProtectedArea.Create(
            "Test MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        context.MarineProtectedAreas.Add(mpa);
        await context.SaveChangesAsync();

        // Add bleaching alerts
        var alert1 = BleachingAlert.Create(
            CreateTestPoint(), 
            new DateOnly(2024, 1, 15), 
            28.5, 
            1.2, 
            5.0, 
            mpaId: mpa.Id);
        
        var alert2 = BleachingAlert.Create(
            CreateTestPoint(), 
            new DateOnly(2024, 1, 20), 
            29.0, 
            1.5, 
            8.0, 
            mpaId: mpa.Id);

        context.BleachingAlerts.AddRange(alert1, alert2);
        await context.SaveChangesAsync();

        var handler = new GetMpaStatusReportDataQueryHandler(context);
        var query = new GetMpaStatusReportDataQuery(mpa.Id, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.BleachingData.TotalAlerts.Should().Be(2);
        result.BleachingData.MaxDegreeHeatingWeeks.Should().Be(8.0);
        result.BleachingData.CriticalAlertsCount.Should().Be(1); // DHW >= 8
        result.BleachingData.RecentAlerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithDateRange_FiltersDataCorrectly()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa = MarineProtectedArea.Create(
            "Test MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        context.MarineProtectedAreas.Add(mpa);
        await context.SaveChangesAsync();

        // Add alerts in different months
        var alertJan = BleachingAlert.Create(CreateTestPoint(), new DateOnly(2024, 1, 15), 28.5, 1.2, 5.0, mpaId: mpa.Id);
        var alertDec = BleachingAlert.Create(CreateTestPoint(), new DateOnly(2023, 12, 15), 27.5, 0.8, 3.0, mpaId: mpa.Id);

        context.BleachingAlerts.AddRange(alertJan, alertDec);
        await context.SaveChangesAsync();

        var handler = new GetMpaStatusReportDataQueryHandler(context);
        var query = new GetMpaStatusReportDataQuery(mpa.Id, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DataFromDate.Should().Be(new DateTime(2024, 1, 1));
        result.DataToDate.Should().Be(new DateTime(2024, 1, 31));
        result.BleachingData.TotalAlerts.Should().Be(1); // Only January alert
    }
}
