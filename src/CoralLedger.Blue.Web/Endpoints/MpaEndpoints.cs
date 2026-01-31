using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.MarineProtectedAreas.Commands.SyncFromWdpa;
using CoralLedger.Blue.Application.Features.MarineProtectedAreas.Queries.GetAllMpas;
using CoralLedger.Blue.Application.Features.MarineProtectedAreas.Queries.GetMpaById;
using CoralLedger.Blue.Application.Features.MarineProtectedAreas.Queries.GetMpasGeoJson;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints;

public static class MpaEndpoints
{
    public static IEndpointRouteBuilder MapMpaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mpas")
            .WithTags("Marine Protected Areas");

        // GET /api/mpas - Get all MPAs (summary)
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var mpas = await mediator.Send(new GetAllMpasQuery(), ct).ConfigureAwait(false);
            return Results.Ok(mpas);
        })
        .WithName("GetAllMpas")
        .WithDescription("Get all Marine Protected Areas with summary information")
        .Produces<IReadOnlyList<CoralLedger.Blue.Application.Features.MarineProtectedAreas.DTOs.MpaSummaryDto>>();

        // GET /api/mpas/geojson - Get all MPAs as GeoJSON FeatureCollection
        // ?resolution=full|medium|low (default: medium)
        group.MapGet("/geojson", async (
            string? resolution,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var res = ParseResolution(resolution);
            var geoJson = await mediator.Send(new GetMpasGeoJsonQuery(res), ct).ConfigureAwait(false);
            return Results.Ok(geoJson);
        })
        .WithName("GetMpasGeoJson")
        .WithDescription("Get all Marine Protected Areas as GeoJSON FeatureCollection for map display. " +
            "Use ?resolution=full|detail|medium|low to control geometry simplification (default: medium). " +
            "Tolerances: full=0, detail=~10m, medium=~100m, low=~1km")
        .Produces<MpaGeoJsonCollection>();

        // GET /api/mpas/{id} - Get specific MPA by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var mpa = await mediator.Send(new GetMpaByIdQuery(id), ct).ConfigureAwait(false);
            return mpa is null ? Results.NotFound() : Results.Ok(mpa);
        })
        .WithName("GetMpaById")
        .WithDescription("Get detailed information about a specific Marine Protected Area")
        .Produces<CoralLedger.Blue.Application.Features.MarineProtectedAreas.DTOs.MpaDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/mpas/{id}/sync-wdpa - Sync MPA boundary from WDPA
        group.MapPost("/{id:guid}/sync-wdpa", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SyncMpaFromWdpaCommand(id), ct).ConfigureAwait(false);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
        .WithName("SyncMpaFromWdpa")
        .WithDescription("Sync MPA boundary geometry from Protected Planet WDPA API")
        .Produces<SyncResult>()
        .Produces<SyncResult>(StatusCodes.Status400BadRequest);

        // GET /api/mpas/nearest?lon={lon}&lat={lat} - Find nearest MPA to a point
        group.MapGet("/nearest", async (
            double lon,
            double lat,
            IMpaProximityService proximityService,
            CancellationToken ct) =>
        {
            var factory = new NetTopologySuite.Geometries.GeometryFactory(
                new NetTopologySuite.Geometries.PrecisionModel(), 4326);
            var point = factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(lon, lat));

            var result = await proximityService.FindNearestMpaAsync(point, ct).ConfigureAwait(false);
            if (result == null)
                return Results.NotFound("No MPAs found");

            return Results.Ok(new
            {
                result.MpaId,
                result.MpaName,
                ProtectionLevel = result.ProtectionLevel.ToString(),
                result.DistanceKm,
                result.IsWithinMpa,
                NearestPoint = result.NearestBoundaryPoint != null
                    ? new { Lon = result.NearestBoundaryPoint.X, Lat = result.NearestBoundaryPoint.Y }
                    : null
            });
        })
        .WithName("GetNearestMpa")
        .WithDescription("Find the nearest Marine Protected Area to a given coordinate")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/mpas/contains?lon={lon}&lat={lat} - Check if point is within an MPA
        group.MapGet("/contains", async (
            double lon,
            double lat,
            IMpaProximityService proximityService,
            CancellationToken ct) =>
        {
            var factory = new NetTopologySuite.Geometries.GeometryFactory(
                new NetTopologySuite.Geometries.PrecisionModel(), 4326);
            var point = factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(lon, lat));

            var result = await proximityService.CheckMpaContainmentAsync(point, ct).ConfigureAwait(false);
            if (result == null)
                return Results.Ok(new { IsWithinMpa = false });

            return Results.Ok(new
            {
                IsWithinMpa = true,
                result.MpaId,
                result.MpaName,
                ProtectionLevel = result.ProtectionLevel.ToString(),
                result.IsNoTakeZone,
                result.DistanceToNearestBoundaryKm,
                result.NearestReefId,
                result.NearestReefName
            });
        })
        .WithName("CheckMpaContainment")
        .WithDescription("Check if a coordinate is within a Marine Protected Area")
        .Produces<object>();

        // GET /api/mpas/within-radius?lon={lon}&lat={lat}&radiusKm={radiusKm} - Find MPAs within radius
        group.MapGet("/within-radius", async (
            double lon,
            double lat,
            double radiusKm,
            IMpaProximityService proximityService,
            CancellationToken ct) =>
        {
            var factory = new NetTopologySuite.Geometries.GeometryFactory(
                new NetTopologySuite.Geometries.PrecisionModel(), 4326);
            var point = factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(lon, lat));

            var results = await proximityService.FindMpasWithinRadiusAsync(point, radiusKm, ct).ConfigureAwait(false);

            return Results.Ok(results.Select(r => new
            {
                r.MpaId,
                r.MpaName,
                ProtectionLevel = r.ProtectionLevel.ToString(),
                r.DistanceKm,
                r.IsWithinMpa
            }));
        })
        .WithName("GetMpasWithinRadius")
        .WithDescription("Find all Marine Protected Areas within a given radius of a coordinate")
        .Produces<object>();

        // GET /api/mpas/stats - Get MPA statistics
        group.MapGet("/stats", async (IMarineDbContext context, CancellationToken ct) =>
        {
            var totalCount = await context.MarineProtectedAreas.CountAsync(ct).ConfigureAwait(false);
            var totalArea = await context.MarineProtectedAreas.SumAsync(m => m.AreaSquareKm, ct).ConfigureAwait(false);

            var byIslandGroup = await context.MarineProtectedAreas
                .GroupBy(m => m.IslandGroup)
                .Select(g => new { islandGroup = g.Key.ToString(), count = g.Count(), areaKm2 = g.Sum(m => m.AreaSquareKm) })
                .ToListAsync(ct).ConfigureAwait(false);

            var byProtectionLevel = await context.MarineProtectedAreas
                .GroupBy(m => m.ProtectionLevel)
                .Select(g => new { level = g.Key.ToString(), count = g.Count() })
                .ToListAsync(ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                totalCount,
                totalAreaKm2 = totalArea,
                byIslandGroup,
                byProtectionLevel
            });
        })
        .WithName("GetMpaStats")
        .WithDescription("Get aggregate statistics about Marine Protected Areas")
        .Produces<object>();

        return endpoints;
    }

    private static GeometryResolution ParseResolution(string? resolution) =>
        resolution?.ToLowerInvariant() switch
        {
            "full" => GeometryResolution.Full,
            "detail" => GeometryResolution.Detail,
            "low" => GeometryResolution.Low,
            _ => GeometryResolution.Medium // Default
        };
}
