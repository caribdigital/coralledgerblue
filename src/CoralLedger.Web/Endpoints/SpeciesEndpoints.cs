using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.Species.Queries.GetAllSpecies;
using CoralLedger.Application.Features.Species.Queries.SearchSpecies;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Web.Endpoints;

public static class SpeciesEndpoints
{
    public static IEndpointRouteBuilder MapSpeciesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/species")
            .WithTags("Bahamian Species");

        // GET /api/species - Get all species
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var species = await mediator.Send(new GetAllSpeciesQuery(), ct);
            return Results.Ok(species);
        })
        .WithName("GetAllSpecies")
        .WithDescription("Get all Bahamian marine species")
        .Produces<IReadOnlyList<SpeciesDto>>();

        // GET /api/species/search - Search species by name or filters
        group.MapGet("/search", async (
            string? q,
            string? category,
            bool? invasive,
            bool? threatened,
            IMediator mediator,
            CancellationToken ct) =>
        {
            SpeciesCategory? categoryEnum = null;
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<SpeciesCategory>(category, true, out var parsed))
            {
                categoryEnum = parsed;
            }

            var query = new SearchSpeciesQuery(q, categoryEnum, invasive, threatened);
            var species = await mediator.Send(query, ct);
            return Results.Ok(species);
        })
        .WithName("SearchSpecies")
        .WithDescription("Search species by name (scientific, common, or local) with optional filters")
        .Produces<IReadOnlyList<SpeciesDto>>();

        // GET /api/species/invasive - Get invasive species
        group.MapGet("/invasive", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new SearchSpeciesQuery(IsInvasive: true);
            var species = await mediator.Send(query, ct);
            return Results.Ok(species);
        })
        .WithName("GetInvasiveSpecies")
        .WithDescription("Get all invasive species (Lionfish, etc.) - high priority for removal")
        .Produces<IReadOnlyList<SpeciesDto>>();

        // GET /api/species/threatened - Get threatened/endangered species
        group.MapGet("/threatened", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new SearchSpeciesQuery(IsThreatened: true);
            var species = await mediator.Send(query, ct);
            return Results.Ok(species);
        })
        .WithName("GetThreatenedSpecies")
        .WithDescription("Get all threatened and endangered species (Vulnerable, Endangered, Critically Endangered)")
        .Produces<IReadOnlyList<SpeciesDto>>();

        // GET /api/species/{id} - Get species by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMarineDbContext context,
            CancellationToken ct) =>
        {
            var species = await context.BahamianSpecies
                .Where(s => s.Id == id)
                .Select(s => new SpeciesDto(
                    s.Id,
                    s.ScientificName,
                    s.CommonName,
                    s.LocalName,
                    s.Category.ToString(),
                    s.ConservationStatus.ToString(),
                    s.IsInvasive,
                    s.IsThreatened,
                    s.Description,
                    s.IdentificationTips,
                    s.Habitat,
                    s.TypicalDepthMinM,
                    s.TypicalDepthMaxM))
                .FirstOrDefaultAsync(ct);

            return species is null ? Results.NotFound() : Results.Ok(species);
        })
        .WithName("GetSpeciesById")
        .WithDescription("Get detailed species information by ID")
        .Produces<SpeciesDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/species/categories - Get list of species categories
        group.MapGet("/categories", () =>
        {
            var categories = Enum.GetValues<SpeciesCategory>()
                .Select(c => new { value = c.ToString(), name = c.ToString() });
            return Results.Ok(categories);
        })
        .WithName("GetSpeciesCategories")
        .WithDescription("Get list of species categories (Fish, Coral, Invertebrate, etc.)");

        // GET /api/species/conservation-statuses - Get list of conservation statuses
        group.MapGet("/conservation-statuses", () =>
        {
            var statuses = Enum.GetValues<ConservationStatus>()
                .Select(s => new { value = s.ToString(), name = FormatConservationStatus(s) });
            return Results.Ok(statuses);
        })
        .WithName("GetConservationStatuses")
        .WithDescription("Get list of IUCN conservation statuses");

        return endpoints;
    }

    private static string FormatConservationStatus(ConservationStatus status) => status switch
    {
        ConservationStatus.NotEvaluated => "Not Evaluated (NE)",
        ConservationStatus.DataDeficient => "Data Deficient (DD)",
        ConservationStatus.LeastConcern => "Least Concern (LC)",
        ConservationStatus.NearThreatened => "Near Threatened (NT)",
        ConservationStatus.Vulnerable => "Vulnerable (VU)",
        ConservationStatus.Endangered => "Endangered (EN)",
        ConservationStatus.CriticallyEndangered => "Critically Endangered (CR)",
        ConservationStatus.ExtinctInWild => "Extinct in the Wild (EW)",
        ConservationStatus.Extinct => "Extinct (EX)",
        _ => status.ToString()
    };
}
