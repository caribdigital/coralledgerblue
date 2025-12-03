using CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetAllMpas;
using CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetMpaById;
using CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetMpasGeoJson;
using MediatR;

namespace CoralLedger.Web.Endpoints;

public static class MpaEndpoints
{
    public static IEndpointRouteBuilder MapMpaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mpas")
            .WithTags("Marine Protected Areas");

        // GET /api/mpas - Get all MPAs (summary)
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var mpas = await mediator.Send(new GetAllMpasQuery(), ct);
            return Results.Ok(mpas);
        })
        .WithName("GetAllMpas")
        .WithDescription("Get all Marine Protected Areas with summary information")
        .Produces<IReadOnlyList<CoralLedger.Application.Features.MarineProtectedAreas.DTOs.MpaSummaryDto>>();

        // GET /api/mpas/geojson - Get all MPAs as GeoJSON FeatureCollection
        group.MapGet("/geojson", async (IMediator mediator, CancellationToken ct) =>
        {
            var geoJson = await mediator.Send(new GetMpasGeoJsonQuery(), ct);
            return Results.Ok(geoJson);
        })
        .WithName("GetMpasGeoJson")
        .WithDescription("Get all Marine Protected Areas as GeoJSON FeatureCollection for map display")
        .Produces<MpaGeoJsonCollection>();

        // GET /api/mpas/{id} - Get specific MPA by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var mpa = await mediator.Send(new GetMpaByIdQuery(id), ct);
            return mpa is null ? Results.NotFound() : Results.Ok(mpa);
        })
        .WithName("GetMpaById")
        .WithDescription("Get detailed information about a specific Marine Protected Area")
        .Produces<CoralLedger.Application.Features.MarineProtectedAreas.DTOs.MpaDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
