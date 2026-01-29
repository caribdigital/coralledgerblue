using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints;

public static class VesselEndpoints
{
    public static IEndpointRouteBuilder MapVesselEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vessels")
            .WithTags("Vessels");

        // GET /api/vessels/search?query=...&flag=...&limit=...
        group.MapGet("/search", async (
            IGlobalFishingWatchClient gfwClient,
            string? query,
            string? flag,
            int limit = 50,
            CancellationToken ct = default) =>
        {
            var vessels = await gfwClient.SearchVesselsAsync(query, flag, null, limit, ct);
            return Results.Ok(vessels);
        })
        .WithName("SearchVessels")
        .WithDescription("Search for vessels using Global Fishing Watch API")
        .Produces<IEnumerable<GfwVesselInfo>>();

        // GET /api/vessels/{vesselId}
        group.MapGet("/{vesselId}", async (
            string vesselId,
            IGlobalFishingWatchClient gfwClient,
            CancellationToken ct = default) =>
        {
            var vessel = await gfwClient.GetVesselByIdAsync(vesselId, ct);
            return vessel is null ? Results.NotFound() : Results.Ok(vessel);
        })
        .WithName("GetVesselById")
        .WithDescription("Get vessel details from Global Fishing Watch")
        .Produces<GfwVesselInfo>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/vessels/fishing-events?minLon=&minLat=&maxLon=&maxLat=&startDate=&endDate=
        group.MapGet("/fishing-events", async (
            IGlobalFishingWatchClient gfwClient,
            double minLon,
            double minLat,
            double maxLon,
            double maxLat,
            DateTime startDate,
            DateTime endDate,
            int limit = 500,
            CancellationToken ct = default) =>
        {
            var events = await gfwClient.GetFishingEventsAsync(
                minLon, minLat, maxLon, maxLat, startDate, endDate, limit, ct);
            return Results.Ok(events);
        })
        .WithName("GetFishingEvents")
        .WithDescription("Get fishing events in a geographic region from Global Fishing Watch")
        .Produces<IEnumerable<GfwEvent>>();

        // GET /api/vessels/fishing-events/bahamas?startDate=&endDate=
        // Returns fishing events from database with MPA violation context
        group.MapGet("/fishing-events/bahamas", async (
            IMarineDbContext dbContext,
            DateTime? startDate,
            DateTime? endDate,
            int limit = 500,
            CancellationToken ct = default) =>
        {
            // Ensure DateTime values are UTC (PostgreSQL requires timestamptz to be UTC)
            var start = startDate.HasValue
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(-30);
            var end = endDate.HasValue
                ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;

            var events = await dbContext.VesselEvents
                .Include(e => e.Vessel)
                .Include(e => e.MarineProtectedArea)
                .Where(e => e.EventType == VesselEventType.Fishing)
                .Where(e => e.StartTime >= start && e.StartTime <= end)
                .OrderByDescending(e => e.StartTime)
                .Take(limit)
                .Select(e => new
                {
                    EventId = e.GfwEventId ?? e.Id.ToString(),
                    VesselId = e.Vessel.GfwVesselId ?? e.VesselId.ToString(),
                    VesselName = e.Vessel.Name,
                    Longitude = e.Location.X,
                    Latitude = e.Location.Y,
                    e.StartTime,
                    e.EndTime,
                    e.DurationHours,
                    e.DistanceKm,
                    EventType = e.EventType.ToString(),
                    e.IsInMpa,
                    MpaName = e.MarineProtectedArea != null ? e.MarineProtectedArea.Name : null
                })
                .ToListAsync(ct);

            return Results.Ok(events);
        })
        .WithName("GetBahamasFishingEvents")
        .WithDescription("Get fishing events in the Bahamas from database with MPA context")
        .Produces<IEnumerable<object>>();

        // GET /api/vessels/encounters?...
        group.MapGet("/encounters", async (
            IGlobalFishingWatchClient gfwClient,
            double minLon,
            double minLat,
            double maxLon,
            double maxLat,
            DateTime startDate,
            DateTime endDate,
            int limit = 500,
            CancellationToken ct = default) =>
        {
            var events = await gfwClient.GetEncountersAsync(
                minLon, minLat, maxLon, maxLat, startDate, endDate, limit, ct);
            return Results.Ok(events);
        })
        .WithName("GetVesselEncounters")
        .WithDescription("Get vessel encounters (meetings at sea) from Global Fishing Watch")
        .Produces<IEnumerable<GfwEvent>>();

        // GET /api/vessels/stats?...
        group.MapGet("/stats", async (
            IGlobalFishingWatchClient gfwClient,
            double minLon,
            double minLat,
            double maxLon,
            double maxLat,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct = default) =>
        {
            var stats = await gfwClient.GetFishingEffortStatsAsync(
                minLon, minLat, maxLon, maxLat, startDate, endDate, ct);
            return Results.Ok(stats);
        })
        .WithName("GetFishingEffortStats")
        .WithDescription("Get fishing effort statistics for a region from Global Fishing Watch")
        .Produces<GfwFishingEffortStats>();

        return endpoints;
    }
}
