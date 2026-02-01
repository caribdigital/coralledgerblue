using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.AddPatrolPoint;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.AddPatrolWaypoint;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.StartPatrolRoute;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.StopPatrolRoute;
using CoralLedger.Blue.Application.Features.PatrolRoutes.DTOs;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Queries.GetPatrolRouteById;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Queries.GetPatrolRoutes;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Services.PatrolExport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints;

public static class PatrolRouteEndpoints
{
    public static IEndpointRouteBuilder MapPatrolRouteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/patrols")
            .WithTags("Patrol Routes");

        // POST /api/patrols/start - Start new patrol route
        group.MapPost("/start", async (
            StartPatrolRouteRequest request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var command = new StartPatrolRouteCommand(
                request.OfficerName,
                request.OfficerId,
                request.Notes,
                request.RecordingIntervalSeconds);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Created($"/api/patrols/{result.PatrolRouteId}", result);
        })
        .WithName("StartPatrolRoute")
        .WithDescription("Start a new patrol route recording")
        .Produces<StartPatrolRouteResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/patrols/{id}/stop - Stop patrol route
        group.MapPost("/{id:guid}/stop", async (
            Guid id,
            StopPatrolRouteRequest? request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var command = new StopPatrolRouteCommand(
                id,
                request?.CompletionNotes,
                request?.Cancel ?? false,
                request?.CancellationReason);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(result);
        })
        .WithName("StopPatrolRoute")
        .WithDescription("Stop or cancel a patrol route")
        .Produces<StopPatrolRouteResult>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/patrols/{id}/points - Add GPS point to patrol
        group.MapPost("/{id:guid}/points", async (
            Guid id,
            AddPatrolPointRequest request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var command = new AddPatrolPointCommand(
                id,
                request.Longitude,
                request.Latitude,
                request.Timestamp,
                request.Accuracy,
                request.Altitude,
                request.Speed,
                request.Heading);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Created($"/api/patrols/{id}/points/{result.PointId}", result);
        })
        .WithName("AddPatrolPoint")
        .WithDescription("Add a GPS tracking point to an active patrol route")
        .Produces<AddPatrolPointResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/patrols/{id}/waypoints - Add waypoint with notes
        group.MapPost("/{id:guid}/waypoints", async (
            Guid id,
            AddPatrolWaypointRequest request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var command = new AddPatrolWaypointCommand(
                id,
                request.Longitude,
                request.Latitude,
                request.Title,
                request.Notes,
                request.WaypointType,
                request.Timestamp);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Created($"/api/patrols/{id}/waypoints/{result.WaypointId}", result);
        })
        .WithName("AddPatrolWaypoint")
        .WithDescription("Add a waypoint with notes to a patrol route")
        .Produces<AddPatrolWaypointResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // GET /api/patrols - List patrol routes
        group.MapGet("/", async (
            IMediator mediator,
            string? officerId,
            PatrolRouteStatus? status,
            Guid? mpaId,
            DateTime? fromDate,
            DateTime? toDate,
            int limit = 100,
            CancellationToken ct = default) =>
        {
            var query = new GetPatrolRoutesQuery(officerId, status, mpaId, fromDate, toDate, limit);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetPatrolRoutes")
        .WithDescription("Get list of patrol routes with optional filters")
        .Produces<IReadOnlyList<PatrolRouteSummaryDto>>();

        // GET /api/patrols/{id} - Get patrol route details
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(new GetPatrolRouteByIdQuery(id), ct).ConfigureAwait(false);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetPatrolRouteById")
        .WithDescription("Get detailed information about a specific patrol route")
        .Produces<PatrolRouteDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/patrols/{id}/geojson - Get patrol route as GeoJSON
        group.MapGet("/{id:guid}/geojson", async (
            Guid id,
            IMarineDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var route = await dbContext.PatrolRoutes
                .AsNoTracking()
                .Include(p => p.Points)
                .Include(p => p.Waypoints)
                .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);

            if (route == null)
            {
                return Results.NotFound();
            }

            var trackPoints = route.Points
                .OrderBy(p => p.Timestamp)
                .Select(p => new[] { p.Location.X, p.Location.Y })
                .ToList();

            var waypointFeatures = route.Waypoints
                .Select(w => new
                {
                    type = "Feature",
                    id = w.Id.ToString(),
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[] { w.Location.X, w.Location.Y }
                    },
                    properties = new
                    {
                        title = w.Title,
                        notes = w.Notes,
                        waypointType = w.WaypointType,
                        timestamp = w.Timestamp
                    }
                })
                .ToList();

            var features = new List<object>();

            // Add track as LineString
            if (trackPoints.Count >= 2)
            {
                features.Add(new
                {
                    type = "Feature",
                    id = route.Id.ToString(),
                    geometry = new
                    {
                        type = "LineString",
                        coordinates = trackPoints
                    },
                    properties = new
                    {
                        officerName = route.OfficerName,
                        startTime = route.StartTime,
                        endTime = route.EndTime,
                        status = route.Status.ToString(),
                        totalDistanceMeters = route.TotalDistanceMeters,
                        durationSeconds = route.DurationSeconds
                    }
                });
            }

            // Add waypoints
            features.AddRange(waypointFeatures);

            return Results.Ok(new
            {
                type = "FeatureCollection",
                features
            });
        })
        .WithName("GetPatrolRouteGeoJson")
        .WithDescription("Get patrol route as GeoJSON for map display")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/patrols/{id}/export/gpx - Export as GPX
        group.MapGet("/{id:guid}/export/gpx", async (
            Guid id,
            IMarineDbContext dbContext,
            IPatrolRouteExportService exportService,
            CancellationToken ct = default) =>
        {
            var route = await dbContext.PatrolRoutes
                .AsNoTracking()
                .Include(p => p.Points)
                .Include(p => p.Waypoints)
                .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);

            if (route == null)
            {
                return Results.NotFound();
            }

            var gpx = exportService.ExportToGpx(route);
            return Results.Content(gpx, "application/gpx+xml", null, StatusCodes.Status200OK);
        })
        .WithName("ExportPatrolRouteGpx")
        .WithDescription("Export patrol route as GPX file")
        .Produces(StatusCodes.Status200OK, contentType: "application/gpx+xml")
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/patrols/{id}/export/kml - Export as KML
        group.MapGet("/{id:guid}/export/kml", async (
            Guid id,
            IMarineDbContext dbContext,
            IPatrolRouteExportService exportService,
            CancellationToken ct = default) =>
        {
            var route = await dbContext.PatrolRoutes
                .AsNoTracking()
                .Include(p => p.Points)
                .Include(p => p.Waypoints)
                .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);

            if (route == null)
            {
                return Results.NotFound();
            }

            var kml = exportService.ExportToKml(route);
            return Results.Content(kml, "application/vnd.google-earth.kml+xml", null, StatusCodes.Status200OK);
        })
        .WithName("ExportPatrolRouteKml")
        .WithDescription("Export patrol route as KML file")
        .Produces(StatusCodes.Status200OK, contentType: "application/vnd.google-earth.kml+xml")
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/patrols/stats - Get patrol coverage statistics
        group.MapGet("/stats", async (
            IMarineDbContext dbContext,
            DateTime? fromDate,
            DateTime? toDate,
            string? officerId,
            CancellationToken ct = default) =>
        {
            var query = dbContext.PatrolRoutes
                .AsNoTracking()
                .Where(p => p.Status == PatrolRouteStatus.Completed);

            if (fromDate.HasValue)
                query = query.Where(p => p.StartTime >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.StartTime <= toDate.Value);

            if (!string.IsNullOrEmpty(officerId))
                query = query.Where(p => p.OfficerId == officerId);

            var routes = await query.ToListAsync(ct).ConfigureAwait(false);

            var stats = new
            {
                totalPatrols = routes.Count,
                totalDistanceMeters = routes.Sum(r => r.TotalDistanceMeters ?? 0),
                totalDurationSeconds = routes.Sum(r => r.DurationSeconds ?? 0),
                averageDistanceMeters = routes.Any() 
                    ? routes.Where(r => r.TotalDistanceMeters.HasValue)
                        .Average(r => r.TotalDistanceMeters!.Value) 
                    : 0,
                averageDurationSeconds = routes.Any() 
                    ? routes.Where(r => r.DurationSeconds.HasValue)
                        .Average(r => r.DurationSeconds!.Value) 
                    : 0,
                uniqueOfficers = routes.Where(r => !string.IsNullOrEmpty(r.OfficerId))
                    .Select(r => r.OfficerId).Distinct().Count(),
                patrolsByOfficer = routes
                    .Where(r => !string.IsNullOrEmpty(r.OfficerName))
                    .GroupBy(r => new { r.OfficerId, r.OfficerName })
                    .Select(g => new
                    {
                        officerId = g.Key.OfficerId,
                        officerName = g.Key.OfficerName,
                        patrolCount = g.Count(),
                        totalDistance = g.Sum(r => r.TotalDistanceMeters ?? 0),
                        totalDuration = g.Sum(r => r.DurationSeconds ?? 0)
                    })
                    .OrderByDescending(x => x.patrolCount)
                    .ToList()
            };

            return Results.Ok(stats);
        })
        .WithName("GetPatrolStats")
        .WithDescription("Get patrol coverage statistics")
        .Produces<object>();

        return endpoints;
    }
}

// Request models
public record StartPatrolRouteRequest(
    string? OfficerName = null,
    string? OfficerId = null,
    string? Notes = null,
    int RecordingIntervalSeconds = 30);

public record StopPatrolRouteRequest(
    string? CompletionNotes = null,
    bool Cancel = false,
    string? CancellationReason = null);

public record AddPatrolPointRequest(
    double Longitude,
    double Latitude,
    DateTime? Timestamp = null,
    double? Accuracy = null,
    double? Altitude = null,
    double? Speed = null,
    double? Heading = null);

public record AddPatrolWaypointRequest(
    double Longitude,
    double Latitude,
    string Title,
    string? Notes = null,
    string? WaypointType = null,
    DateTime? Timestamp = null);
