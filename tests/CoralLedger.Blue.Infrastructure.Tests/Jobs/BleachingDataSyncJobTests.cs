using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Common.Models;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using CoralLedger.Blue.Infrastructure.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Quartz;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Jobs;

public class BleachingDataSyncJobTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    [Fact]
    public void JobKey_IsCorrectlyDefined()
    {
        // Assert
        BleachingDataSyncJob.Key.Name.Should().Be("BleachingDataSyncJob");
        BleachingDataSyncJob.Key.Group.Should().Be("DataSync");
    }

    [Fact]
    public async Task Execute_WithNoMpas_CompletesSuccessfully()
    {
        // Arrange
        var mockClient = new Mock<ICoralReefWatchClient>();
        var mockLogger = new Mock<ILogger<BleachingDataSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new BleachingDataSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert - should complete without errors
        mockClient.Verify(c => c.GetBleachingDataAsync(
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithMultipleMpas_ProcessesConcurrently()
    {
        // Arrange
        var mockClient = new Mock<ICoralReefWatchClient>();
        mockClient.Setup(c => c.GetBleachingDataAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CrwBleachingData?>.Ok(new CrwBleachingData
            {
                SeaSurfaceTemperature = 28.5,
                SstAnomaly = 1.2,
                DegreeHeatingWeek = 2.3,
                HotSpot = 0.5,
                Longitude = -77.0,
                Latitude = 24.0,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                AlertLevel = 2
            }));

        var mockLogger = new Mock<ILogger<BleachingDataSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Pre-populate with test MPAs
        using (var scope = scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
            
            for (int i = 0; i < 5; i++)
            {
                var mpa = MarineProtectedArea.Create(
                    Guid.NewGuid(),
                    $"Test MPA {i}",
                    GeometryFactory.CreatePolygon(new[]
                    {
                        new Coordinate(-77.0 - i, 24.0),
                        new Coordinate(-77.0 - i, 25.0),
                        new Coordinate(-76.0 - i, 25.0),
                        new Coordinate(-76.0 - i, 24.0),
                        new Coordinate(-77.0 - i, 24.0)
                    }),
                    ProtectionLevel.NoTake,
                    IslandGroup.NewProvidence);
                dbContext.MarineProtectedAreas.Add(mpa);
            }
            await dbContext.SaveChangesAsync();
        }

        var job = new BleachingDataSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert - should have called client for each MPA
        mockClient.Verify(c => c.GetBleachingDataAsync(
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Exactly(5));

        // Verify bleaching alerts were created
        using var verifyScope = scopeFactory.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<MarineDbContext>();
        var alerts = await verifyDbContext.BleachingAlerts.ToListAsync();
        alerts.Should().HaveCount(5);
    }

    [Fact]
    public async Task Execute_WithExistingAlert_UpdatesMetrics()
    {
        // Arrange
        var mockClient = new Mock<ICoralReefWatchClient>();
        mockClient.Setup(c => c.GetBleachingDataAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CrwBleachingData?>.Ok(new CrwBleachingData
            {
                SeaSurfaceTemperature = 29.0,
                SstAnomaly = 1.5,
                DegreeHeatingWeek = 3.0,
                HotSpot = 1.0,
                Longitude = -77.0,
                Latitude = 24.0,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                AlertLevel = 3
            }));

        var mockLogger = new Mock<ILogger<BleachingDataSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        Guid mpaId;
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        
        // Pre-populate with MPA and existing alert
        using (var scope = scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
            
            var mpa = MarineProtectedArea.Create(
                Guid.NewGuid(),
                "Test MPA",
                GeometryFactory.CreatePolygon(new[]
                {
                    new Coordinate(-77.0, 24.0),
                    new Coordinate(-77.0, 25.0),
                    new Coordinate(-76.0, 25.0),
                    new Coordinate(-76.0, 24.0),
                    new Coordinate(-77.0, 24.0)
                }),
                ProtectionLevel.NoTake,
                IslandGroup.NewProvidence);
            mpaId = mpa.Id;
            dbContext.MarineProtectedAreas.Add(mpa);

            var existingAlert = BleachingAlert.Create(Guid.NewGuid(), 
                mpa.Centroid,
                targetDate,
                27.0,
                0.5,
                1.0,
                0.2,
                mpaId);
            dbContext.BleachingAlerts.Add(existingAlert);
            
            await dbContext.SaveChangesAsync();
        }

        var job = new BleachingDataSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert - should have updated existing alert
        using var verifyScope = scopeFactory.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var alerts = await verifyDbContext.BleachingAlerts
            .Where(a => a.MarineProtectedAreaId == mpaId)
            .ToListAsync();
        
        alerts.Should().HaveCount(1);
        alerts[0].SeaSurfaceTemperature.Should().Be(29.0);
        alerts[0].SstAnomaly.Should().Be(1.5);
        alerts[0].DegreeHeatingWeek.Should().Be(3.0);
        alerts[0].HotSpot.Should().Be(1.0);
    }

    private static IServiceProvider CreateServiceProvider(
        ICoralReefWatchClient crwClient,
        ILogger<BleachingDataSyncJob> logger)
    {
        var services = new ServiceCollection();

        // Use in-memory database - capture dbName outside lambda so all scopes share the same database
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<MarineDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName: dbName);
        }, ServiceLifetime.Scoped);

        services.AddSingleton<ICoralReefWatchClient>(crwClient);
        services.AddSingleton<ILogger<BleachingDataSyncJob>>(logger);

        return services.BuildServiceProvider();
    }

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
