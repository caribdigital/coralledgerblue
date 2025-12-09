using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.Species.Queries.GetAllSpecies;
using CoralLedger.Application.Features.Species.Queries.SearchSpecies;
using CoralLedger.Domain.Entities;
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

        // POST /api/species/misidentification - Report a species misidentification
        // Sprint 4.3 US-4.3.4: 'Report Misidentification' feedback loop to improve the model
        group.MapPost("/misidentification", async (
            ReportMisidentificationRequest request,
            IMarineDbContext context,
            CancellationToken ct) =>
        {
            // Validate the species observation exists
            var observation = await context.SpeciesObservations
                .FirstOrDefaultAsync(o => o.Id == request.SpeciesObservationId, ct);

            if (observation is null)
            {
                return Results.NotFound(new { error = "Species observation not found" });
            }

            // Check for corrected species if ID provided
            Guid? correctedSpeciesId = null;
            if (!string.IsNullOrEmpty(request.CorrectedScientificName))
            {
                var correctedSpecies = await context.BahamianSpecies
                    .FirstOrDefaultAsync(s =>
                        s.ScientificName.ToLower() == request.CorrectedScientificName.ToLower(), ct);
                correctedSpeciesId = correctedSpecies?.Id;
            }

            // Parse expertise enum
            if (!Enum.TryParse<ReporterExpertise>(request.Expertise, true, out var expertise))
            {
                expertise = ReporterExpertise.CitizenScientist;
            }

            // Create the report
            var report = SpeciesMisidentificationReport.Create(
                request.SpeciesObservationId,
                request.IncorrectScientificName,
                request.Reason,
                request.CorrectedScientificName,
                correctedSpeciesId,
                request.ReporterEmail,
                request.ReporterName,
                expertise);

            context.MisidentificationReports.Add(report);
            await context.SaveChangesAsync(ct);

            return Results.Created($"/api/species/misidentification/{report.Id}", new
            {
                reportId = report.Id,
                status = report.Status.ToString(),
                message = "Thank you for your feedback. Your report will be reviewed by our team."
            });
        })
        .WithName("ReportMisidentification")
        .WithDescription("Report an AI species misidentification (Sprint 4.3 US-4.3.4 feedback loop)")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/species/misidentification - Get pending misidentification reports (moderator view)
        group.MapGet("/misidentification", async (
            string? status,
            int limit,
            IMarineDbContext context,
            CancellationToken ct) =>
        {
            var query = context.MisidentificationReports
                .AsNoTracking()
                .Include(r => r.SpeciesObservation)
                .Include(r => r.CorrectedSpecies)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<MisidentificationReportStatus>(status, true, out var statusEnum))
            {
                query = query.Where(r => r.Status == statusEnum);
            }
            else
            {
                // Default to pending reports
                query = query.Where(r => r.Status == MisidentificationReportStatus.Pending);
            }

            var reports = await query
                .OrderByDescending(r => r.ReportedAt)
                .Take(limit > 0 ? limit : 50)
                .Select(r => new MisidentificationReportDto(
                    r.Id,
                    r.SpeciesObservationId,
                    r.IncorrectScientificName,
                    r.CorrectedScientificName,
                    r.CorrectedSpeciesId,
                    r.Reason,
                    r.ReporterName,
                    r.Expertise.ToString(),
                    r.Status.ToString(),
                    r.ReportedAt,
                    r.ReviewedAt,
                    r.ReviewNotes))
                .ToListAsync(ct);

            return Results.Ok(reports);
        })
        .WithName("GetMisidentificationReports")
        .WithDescription("Get species misidentification reports (for moderators)")
        .Produces<IReadOnlyList<MisidentificationReportDto>>();

        // PATCH /api/species/misidentification/{id}/review - Review a misidentification report
        group.MapPatch("/misidentification/{id:guid}/review", async (
            Guid id,
            ReviewMisidentificationRequest request,
            IMarineDbContext context,
            CancellationToken ct) =>
        {
            var report = await context.MisidentificationReports
                .FirstOrDefaultAsync(r => r.Id == id, ct);

            if (report is null)
            {
                return Results.NotFound(new { error = "Misidentification report not found" });
            }

            if (!Enum.TryParse<MisidentificationReportStatus>(request.Status, true, out var newStatus))
            {
                return Results.BadRequest(new { error = "Invalid status. Use: Confirmed, Rejected, or Inconclusive" });
            }

            report.MarkAsReviewed(newStatus, request.ReviewNotes);
            await context.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                reportId = report.Id,
                status = report.Status.ToString(),
                reviewedAt = report.ReviewedAt
            });
        })
        .WithName("ReviewMisidentification")
        .WithDescription("Review and update a misidentification report status")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);

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

/// <summary>
/// Request to report a species misidentification
/// </summary>
public record ReportMisidentificationRequest(
    Guid SpeciesObservationId,
    string IncorrectScientificName,
    string Reason,
    string? CorrectedScientificName = null,
    string? ReporterEmail = null,
    string? ReporterName = null,
    string? Expertise = "CitizenScientist");

/// <summary>
/// Request to review a misidentification report
/// </summary>
public record ReviewMisidentificationRequest(
    string Status,
    string? ReviewNotes = null);

/// <summary>
/// DTO for misidentification report
/// </summary>
public record MisidentificationReportDto(
    Guid Id,
    Guid SpeciesObservationId,
    string IncorrectScientificName,
    string? CorrectedScientificName,
    Guid? CorrectedSpeciesId,
    string Reason,
    string? ReporterName,
    string Expertise,
    string Status,
    DateTime ReportedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes);
