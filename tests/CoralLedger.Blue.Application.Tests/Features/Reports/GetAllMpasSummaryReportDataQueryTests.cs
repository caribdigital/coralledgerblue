using CoralLedger.Blue.Application.Features.Reports.Queries;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Application.Tests.Features.Reports;

public class GetAllMpasSummaryReportDataQueryTests
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
    public async Task Handle_WithMultipleMpas_ReturnsAllMpasSummary()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa1 = MarineProtectedArea.Create(
            "Exuma Cays Land and Sea Park",
            CreateTestPolygon(-77.5, 24.5),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var mpa2 = MarineProtectedArea.Create(
            "Andros West Side National Park",
            CreateTestPolygon(-78.0, 25.0),
            ProtectionLevel.HighlyProtected,
            IslandGroup.Andros);

        var mpa3 = MarineProtectedArea.Create(
            "Lucayan National Park",
            CreateTestPolygon(-78.5, 26.5),
            ProtectionLevel.LightlyProtected,
            IslandGroup.GrandBahama);

        context.MarineProtectedAreas.AddRange(mpa1, mpa2, mpa3);
        await context.SaveChangesAsync();

        var handler = new GetAllMpasSummaryReportDataQueryHandler(context);
        var query = new GetAllMpasSummaryReportDataQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalMpas.Should().Be(3);
        result.Mpas.Should().HaveCount(3);
        result.Statistics.Should().NotBeNull();
        result.Statistics.ActiveMpas.Should().Be(3);
        result.Statistics.MpasByIslandGroup.Should().ContainKey("Exumas");
        result.Statistics.MpasByIslandGroup.Should().ContainKey("Andros");
        result.Statistics.MpasByIslandGroup.Should().ContainKey("GrandBahama");
        result.Statistics.MpasByProtectionLevel.Should().ContainKey("NoTake");
        result.Statistics.MpasByProtectionLevel.Should().ContainKey("HighlyProtected");
        result.Statistics.MpasByProtectionLevel.Should().ContainKey("LightlyProtected");
    }

    [Fact]
    public async Task Handle_WithNoMpas_ReturnsEmptyReport()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var handler = new GetAllMpasSummaryReportDataQueryHandler(context);
        var query = new GetAllMpasSummaryReportDataQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalMpas.Should().Be(0);
        result.TotalAreaSquareKm.Should().Be(0);
        result.Mpas.Should().BeEmpty();
        result.Statistics.ActiveMpas.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithIslandGroupFilter_FiltersCorrectly()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa1 = MarineProtectedArea.Create(
            "Exuma MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var mpa2 = MarineProtectedArea.Create(
            "Andros MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Andros);

        context.MarineProtectedAreas.AddRange(mpa1, mpa2);
        await context.SaveChangesAsync();

        var handler = new GetAllMpasSummaryReportDataQueryHandler(context);
        var query = new GetAllMpasSummaryReportDataQuery(IslandGroup: "Exumas");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalMpas.Should().Be(1);
        result.Mpas.Should().HaveCount(1);
        result.Mpas[0].Name.Should().Be("Exuma MPA");
        result.Mpas[0].IslandGroup.Should().Be("Exumas");
    }

    [Fact]
    public async Task Handle_WithProtectionLevelFilter_FiltersCorrectly()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa1 = MarineProtectedArea.Create(
            "No-Take MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var mpa2 = MarineProtectedArea.Create(
            "Highly Protected MPA",
            CreateTestPolygon(),
            ProtectionLevel.HighlyProtected,
            IslandGroup.Exumas);

        context.MarineProtectedAreas.AddRange(mpa1, mpa2);
        await context.SaveChangesAsync();

        var handler = new GetAllMpasSummaryReportDataQueryHandler(context);
        var query = new GetAllMpasSummaryReportDataQuery(ProtectionLevel: "NoTake");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalMpas.Should().Be(1);
        result.Mpas.Should().HaveCount(1);
        result.Mpas[0].Name.Should().Be("No-Take MPA");
        result.Mpas[0].ProtectionLevel.Should().Be("NoTake");
    }

    [Fact]
    public async Task Handle_WithBleachingAndVesselData_AggregatesCorrectly()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        
        var mpa1 = MarineProtectedArea.Create(
            "Test MPA 1",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var mpa2 = MarineProtectedArea.Create(
            "Test MPA 2",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Andros);

        context.MarineProtectedAreas.AddRange(mpa1, mpa2);
        await context.SaveChangesAsync();

        // Add bleaching alerts
        var alert1 = BleachingAlert.Create(CreateTestPoint(), new DateOnly(2024, 1, 15), 28.5, 1.2, 5.0, mpaId: mpa1.Id);
        var alert2 = BleachingAlert.Create(CreateTestPoint(), new DateOnly(2024, 1, 20), 29.0, 1.5, 8.0, mpaId: mpa1.Id);
        var alert3 = BleachingAlert.Create(CreateTestPoint(), new DateOnly(2024, 1, 22), 28.0, 1.0, 4.0, mpaId: mpa2.Id);

        context.BleachingAlerts.AddRange(alert1, alert2, alert3);
        await context.SaveChangesAsync();

        var handler = new GetAllMpasSummaryReportDataQueryHandler(context);
        var query = new GetAllMpasSummaryReportDataQuery(
            FromDate: new DateTime(2024, 1, 1),
            ToDate: new DateTime(2024, 1, 31));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalBleachingAlerts.Should().Be(3);
        
        var mpa1Summary = result.Mpas.First(m => m.Name == "Test MPA 1");
        mpa1Summary.TotalAlerts.Should().Be(2);
        
        var mpa2Summary = result.Mpas.First(m => m.Name == "Test MPA 2");
        mpa2Summary.TotalAlerts.Should().Be(1);
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

        var handler = new GetAllMpasSummaryReportDataQueryHandler(context);
        var query = new GetAllMpasSummaryReportDataQuery(
            FromDate: new DateTime(2024, 1, 1),
            ToDate: new DateTime(2024, 1, 31));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DataFromDate.Should().Be(new DateTime(2024, 1, 1));
        result.DataToDate.Should().Be(new DateTime(2024, 1, 31));
        result.Statistics.TotalBleachingAlerts.Should().Be(1); // Only January alert
        
        var mpaSummary = result.Mpas.First();
        mpaSummary.TotalAlerts.Should().Be(1); // Only January alert
    }
}
