using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Quartz;
using Xunit;

namespace CoralLedger.Infrastructure.Tests.Jobs;

public class VesselEventSyncJobTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    [Fact]
    public void JobKey_IsCorrectlyDefined()
    {
        // Assert
        VesselEventSyncJob.Key.Name.Should().Be("VesselEventSyncJob");
        VesselEventSyncJob.Key.Group.Should().Be("DataSync");
    }

    [Fact]
    public async Task Execute_WhenClientNotConfigured_SkipsSync()
    {
        // Arrange
        var mockClient = new Mock<IGlobalFishingWatchClient>();
        mockClient.Setup(c => c.IsConfigured).Returns(false);

        var mockLogger = new Mock<ILogger<VesselEventSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new VesselEventSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert
        mockClient.Verify(c => c.GetFishingEventsAsync(
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("skipped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WhenConfigured_FetchesEventsFromApi()
    {
        // Arrange
        var mockClient = new Mock<IGlobalFishingWatchClient>();
        mockClient.Setup(c => c.IsConfigured).Returns(true);
        mockClient.Setup(c => c.GetFishingEventsAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GfwEvent>());

        var mockLogger = new Mock<ILogger<VesselEventSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new VesselEventSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert
        mockClient.Verify(c => c.GetFishingEventsAsync(
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            1000, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_WithNewEvents_CreatesVesselsAndEvents()
    {
        // Arrange
        var gfwEvents = new List<GfwEvent>
        {
            new()
            {
                EventId = "event-001",
                VesselId = "vessel-001",
                VesselName = "Test Vessel",
                Longitude = -77.5,
                Latitude = 24.5,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow.AddHours(-1),
                DurationHours = 1.0,
                DistanceKm = 5.5
            }
        };

        var mockClient = new Mock<IGlobalFishingWatchClient>();
        mockClient.Setup(c => c.IsConfigured).Returns(true);
        mockClient.Setup(c => c.GetFishingEventsAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gfwEvents);

        var mockLogger = new Mock<ILogger<VesselEventSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new VesselEventSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert - verify data was saved
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();

        var vessels = await dbContext.Vessels.ToListAsync();
        vessels.Should().HaveCount(1);
        vessels[0].Name.Should().Be("Test Vessel");
        vessels[0].GfwVesselId.Should().Be("vessel-001");
        vessels[0].VesselType.Should().Be(VesselType.Fishing);

        var events = await dbContext.VesselEvents.ToListAsync();
        events.Should().HaveCount(1);
        events[0].GfwEventId.Should().Be("event-001");
        events[0].EventType.Should().Be(VesselEventType.Fishing);
        events[0].DurationHours.Should().Be(1.0);
        events[0].DistanceKm.Should().Be(5.5);
    }

    [Fact]
    public async Task Execute_WithDuplicateEvents_SkipsDuplicates()
    {
        // Arrange
        var gfwEvents = new List<GfwEvent>
        {
            new()
            {
                EventId = "event-duplicate",
                VesselId = "vessel-001",
                VesselName = "Test Vessel",
                Longitude = -77.5,
                Latitude = 24.5,
                StartTime = DateTime.UtcNow.AddHours(-2)
            }
        };

        var mockClient = new Mock<IGlobalFishingWatchClient>();
        mockClient.Setup(c => c.IsConfigured).Returns(true);
        mockClient.Setup(c => c.GetFishingEventsAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gfwEvents);

        var mockLogger = new Mock<ILogger<VesselEventSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Pre-populate with existing event
        using (var scope = scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
            var vessel = Vessel.Create("Existing Vessel", gfwVesselId: "vessel-001");
            dbContext.Vessels.Add(vessel);

            var existingEvent = VesselEvent.CreateFishingEvent(
                vessel.Id,
                GeometryFactory.CreatePoint(new Coordinate(-77.5, 24.5)),
                DateTime.UtcNow.AddDays(-1),
                null, null, null,
                "event-duplicate");
            dbContext.VesselEvents.Add(existingEvent);
            await dbContext.SaveChangesAsync();
        }

        var job = new VesselEventSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert - should still only have 1 event (duplicate skipped)
        using var verifyScope = scopeFactory.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<MarineDbContext>();
        var events = await verifyDbContext.VesselEvents.ToListAsync();
        events.Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_WithExistingVessel_ReusesVessel()
    {
        // Arrange
        var gfwEvents = new List<GfwEvent>
        {
            new()
            {
                EventId = "event-new",
                VesselId = "vessel-existing",
                VesselName = "Updated Name",
                Longitude = -77.5,
                Latitude = 24.5,
                StartTime = DateTime.UtcNow
            }
        };

        var mockClient = new Mock<IGlobalFishingWatchClient>();
        mockClient.Setup(c => c.IsConfigured).Returns(true);
        mockClient.Setup(c => c.GetFishingEventsAsync(
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gfwEvents);

        var mockLogger = new Mock<ILogger<VesselEventSyncJob>>();
        var serviceProvider = CreateServiceProvider(mockClient.Object, mockLogger.Object);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Pre-populate with existing vessel
        Guid existingVesselId;
        using (var scope = scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
            var vessel = Vessel.Create("Original Name", gfwVesselId: "vessel-existing");
            existingVesselId = vessel.Id;
            dbContext.Vessels.Add(vessel);
            await dbContext.SaveChangesAsync();
        }

        var job = new VesselEventSyncJob(scopeFactory, mockLogger.Object);
        var context = CreateMockJobExecutionContext();

        // Act
        await job.Execute(context);

        // Assert - should reuse existing vessel
        using var verifyScope = scopeFactory.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<MarineDbContext>();

        var vessels = await verifyDbContext.Vessels.ToListAsync();
        vessels.Should().HaveCount(1);

        var events = await verifyDbContext.VesselEvents.ToListAsync();
        events.Should().HaveCount(1);
        events[0].VesselId.Should().Be(existingVesselId);
    }

    private static IServiceProvider CreateServiceProvider(
        IGlobalFishingWatchClient gfwClient,
        ILogger<VesselEventSyncJob> logger)
    {
        var services = new ServiceCollection();

        // Use in-memory database
        var dbContextOptions = new DbContextOptionsBuilder<MarineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        services.AddSingleton(dbContextOptions);
        services.AddDbContext<MarineDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
        }, ServiceLifetime.Scoped);

        services.AddSingleton(gfwClient);
        services.AddSingleton(logger);

        return services.BuildServiceProvider();
    }

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
