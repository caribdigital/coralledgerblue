using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Quartz;

namespace CoralLedger.Blue.Infrastructure.Jobs;

/// <summary>
/// Scheduled job that syncs fishing events from Global Fishing Watch API
/// for the Bahamas region and persists vessels/events to the database.
/// Detects fishing activity within Marine Protected Areas.
/// </summary>
[DisallowConcurrentExecution]
public class VesselEventSyncJob : IJob
{
    public static readonly JobKey Key = new("VesselEventSyncJob", "DataSync");

    // Bahamas bounding box
    private const double BahamasMinLon = -80.5;
    private const double BahamasMinLat = 20.5;
    private const double BahamasMaxLon = -72.5;
    private const double BahamasMaxLat = 27.5;

    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VesselEventSyncJob> _logger;

    public VesselEventSyncJob(
        IServiceScopeFactory scopeFactory,
        ILogger<VesselEventSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting VesselEventSyncJob at {Time}", DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        var gfwClient = scope.ServiceProvider.GetRequiredService<IGlobalFishingWatchClient>();

        // Check if client is configured
        if (!gfwClient.IsConfigured)
        {
            _logger.LogWarning(
                "VesselEventSyncJob skipped: GlobalFishingWatch API is not configured. " +
                "Set the API token using: dotnet user-secrets set \"GlobalFishingWatch:ApiToken\" \"your-token\" --project src/CoralLedger.Blue.Web");
            return;
        }

        var syncStats = new SyncStatistics();

        try
        {
            // Define sync window: last 7 days
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-7);

            _logger.LogInformation(
                "Fetching fishing events for Bahamas region from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                startDate, endDate);

            // Fetch fishing events from GFW API
            var events = await gfwClient.GetFishingEventsAsync(
                BahamasMinLon, BahamasMinLat,
                BahamasMaxLon, BahamasMaxLat,
                startDate, endDate,
                limit: 1000,
                context.CancellationToken);

            var eventList = events.ToList();
            _logger.LogInformation("Retrieved {Count} fishing events from GFW API", eventList.Count);

            // Process each event
            foreach (var gfwEvent in eventList)
            {
                try
                {
                    await ProcessEventAsync(dbContext, gfwEvent, syncStats, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    syncStats.FailedEvents++;
                    _logger.LogWarning(ex, "Failed to process event {EventId}", gfwEvent.EventId);
                }
            }

            // Save all changes in single transaction
            await dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "VesselEventSyncJob completed. Events: {Synced} synced, {Skipped} skipped (duplicates), {Failed} failed. " +
                "Vessels: {Created} created, {Updated} updated. MPA violations: {MpaViolations}",
                syncStats.SyncedEvents, syncStats.SkippedEvents, syncStats.FailedEvents,
                syncStats.VesselsCreated, syncStats.VesselsUpdated, syncStats.MpaViolations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VesselEventSyncJob failed with critical error");
            throw;
        }
    }

    private async Task ProcessEventAsync(
        MarineDbContext dbContext,
        GfwEvent gfwEvent,
        SyncStatistics stats,
        CancellationToken ct)
    {
        // Check for duplicate event by GfwEventId
        if (await EventExistsAsync(dbContext, gfwEvent.EventId, ct))
        {
            stats.SkippedEvents++;
            return;
        }

        // Get or create vessel
        var (vessel, isNewVessel) = await GetOrCreateVesselAsync(
            dbContext, gfwEvent.VesselId, gfwEvent.VesselName, ct);

        if (isNewVessel)
            stats.VesselsCreated++;
        else
            stats.VesselsUpdated++;

        // Create location point
        var location = GeometryFactory.CreatePoint(
            new Coordinate(gfwEvent.Longitude, gfwEvent.Latitude));

        // Check MPA intersection
        var (isInMpa, mpaId) = await CheckMpaIntersectionAsync(dbContext, location, ct);
        if (isInMpa)
            stats.MpaViolations++;

        // Create VesselEvent
        var vesselEvent = VesselEvent.CreateFishingEvent(
            vessel.Id,
            location,
            gfwEvent.StartTime,
            gfwEvent.EndTime,
            gfwEvent.DurationHours,
            gfwEvent.DistanceKm,
            gfwEvent.EventId);

        // Set MPA context
        vesselEvent.SetMpaContext(isInMpa, mpaId);

        dbContext.VesselEvents.Add(vesselEvent);
        stats.SyncedEvents++;

        _logger.LogDebug(
            "Created fishing event for vessel {VesselName} at ({Lon:F4}, {Lat:F4}). In MPA: {InMpa}",
            vessel.Name, gfwEvent.Longitude, gfwEvent.Latitude, isInMpa);
    }

    private async Task<bool> EventExistsAsync(
        MarineDbContext dbContext,
        string gfwEventId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(gfwEventId))
            return false;

        return await dbContext.VesselEvents
            .AnyAsync(e => e.GfwEventId == gfwEventId, ct);
    }

    private async Task<(Vessel Vessel, bool IsNew)> GetOrCreateVesselAsync(
        MarineDbContext dbContext,
        string gfwVesselId,
        string? vesselName,
        CancellationToken ct)
    {
        // Try to find by GFW Vessel ID
        var vessel = await dbContext.Vessels
            .FirstOrDefaultAsync(v => v.GfwVesselId == gfwVesselId, ct);

        if (vessel is not null)
            return (vessel, false);

        // Create new vessel
        vessel = Vessel.Create(
            name: vesselName ?? "Unknown Vessel",
            gfwVesselId: gfwVesselId,
            vesselType: VesselType.Fishing);

        dbContext.Vessels.Add(vessel);

        _logger.LogDebug("Created new vessel: {VesselName} (GFW ID: {GfwId})", vessel.Name, gfwVesselId);

        return (vessel, true);
    }

    private async Task<(bool IsInMpa, Guid? MpaId)> CheckMpaIntersectionAsync(
        MarineDbContext dbContext,
        Point location,
        CancellationToken ct)
    {
        // Use PostGIS spatial query to find MPA containing the point
        var mpa = await dbContext.MarineProtectedAreas
            .Where(m => m.Boundary.Contains(location))
            .Select(m => new { m.Id, m.Name })
            .FirstOrDefaultAsync(ct);

        if (mpa is not null)
        {
            _logger.LogInformation(
                "Fishing event detected within MPA: {MpaName} ({MpaId})",
                mpa.Name, mpa.Id);
            return (true, mpa.Id);
        }

        return (false, null);
    }

    private class SyncStatistics
    {
        public int SyncedEvents { get; set; }
        public int SkippedEvents { get; set; }
        public int FailedEvents { get; set; }
        public int VesselsCreated { get; set; }
        public int VesselsUpdated { get; set; }
        public int MpaViolations { get; set; }
    }
}
