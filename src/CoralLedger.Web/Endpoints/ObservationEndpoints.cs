using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.Observations.Commands.CreateObservation;
using CoralLedger.Application.Features.Observations.Queries.GetObservationById;
using CoralLedger.Application.Features.Observations.Queries.GetObservations;
using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Web.Endpoints;

public static class ObservationEndpoints
{
    public static IEndpointRouteBuilder MapObservationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/observations")
            .WithTags("Citizen Observations");

        // GET /api/observations - List observations with filters
        group.MapGet("/", async (
            IMediator mediator,
            ObservationType? type,
            ObservationStatus? status,
            Guid? mpaId,
            DateTime? fromDate,
            DateTime? toDate,
            int limit = 100,
            CancellationToken ct = default) =>
        {
            var query = new GetObservationsQuery(type, status, mpaId, fromDate, toDate, limit);
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetObservations")
        .WithDescription("Get citizen observations with optional filters")
        .Produces<IReadOnlyList<ObservationSummaryDto>>();

        // GET /api/observations/{id} - Get observation details
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(new GetObservationByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetObservationById")
        .WithDescription("Get detailed information about a specific observation")
        .Produces<ObservationDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/observations - Create new observation
        group.MapPost("/", async (
            CreateObservationRequest request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var command = new CreateObservationCommand(
                request.Longitude,
                request.Latitude,
                request.ObservationTime,
                request.Title,
                request.Type,
                request.Description,
                request.Severity,
                request.CitizenEmail,
                request.CitizenName);

            var result = await mediator.Send(command, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Created($"/api/observations/{result.ObservationId}", result);
        })
        .WithName("CreateObservation")
        .WithDescription("Submit a new citizen science observation")
        .Produces<CreateObservationResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/observations/{id}/photos - Upload photo for observation
        group.MapPost("/{id:guid}/photos", async (
            Guid id,
            IFormFile file,
            string? caption,
            IMarineDbContext dbContext,
            IBlobStorageService blobStorage,
            CancellationToken ct = default) =>
        {
            // Verify observation exists
            var observation = await dbContext.CitizenObservations
                .Include(o => o.Photos)
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (observation is null)
            {
                return Results.NotFound(new { error = "Observation not found" });
            }

            // Validate file
            if (file.Length == 0)
            {
                return Results.BadRequest(new { error = "File is empty" });
            }

            if (file.Length > 10 * 1024 * 1024) // 10MB limit
            {
                return Results.BadRequest(new { error = "File size exceeds 10MB limit" });
            }

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/heic" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                return Results.BadRequest(new { error = "Only JPEG, PNG, WebP, and HEIC images are allowed" });
            }

            // Upload to blob storage
            using var stream = file.OpenReadStream();
            var uploadResult = await blobStorage.UploadPhotoAsync(
                stream, file.FileName, file.ContentType, ct);

            if (!uploadResult.Success)
            {
                return Results.Problem(uploadResult.Error ?? "Failed to upload photo", statusCode: 500);
            }

            // Create photo record
            var displayOrder = observation.Photos.Count;
            var photo = ObservationPhoto.Create(
                id,
                uploadResult.BlobName!,
                uploadResult.BlobUri!,
                file.ContentType,
                uploadResult.FileSizeBytes!.Value,
                caption,
                displayOrder);

            observation.AddPhoto(photo);
            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/observations/{id}/photos/{photo.Id}", new
            {
                id = photo.Id,
                blobUri = photo.BlobUri,
                caption = photo.Caption,
                displayOrder = photo.DisplayOrder
            });
        })
        .WithName("UploadObservationPhoto")
        .WithDescription("Upload a photo for an observation")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        // GET /api/observations/geojson - Get observations as GeoJSON
        group.MapGet("/geojson", async (
            IMarineDbContext dbContext,
            ObservationType? type,
            ObservationStatus? status,
            int limit = 500,
            CancellationToken ct = default) =>
        {
            var query = dbContext.CitizenObservations
                .AsNoTracking()
                .Where(o => o.Status == ObservationStatus.Approved || status.HasValue);

            if (type.HasValue)
                query = query.Where(o => o.Type == type.Value);

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var observations = await query
                .OrderByDescending(o => o.ObservationTime)
                .Take(limit)
                .Select(o => new
                {
                    o.Id,
                    Lon = o.Location.X,
                    Lat = o.Location.Y,
                    o.Title,
                    o.Type,
                    o.Severity,
                    o.ObservationTime
                })
                .ToListAsync(ct);

            var features = observations.Select(o => new
            {
                type = "Feature",
                id = o.Id.ToString(),
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { o.Lon, o.Lat }
                },
                properties = new
                {
                    title = o.Title,
                    observationType = o.Type.ToString(),
                    severity = o.Severity,
                    observationTime = o.ObservationTime
                }
            });

            return Results.Ok(new
            {
                type = "FeatureCollection",
                features
            });
        })
        .WithName("GetObservationsGeoJson")
        .WithDescription("Get approved observations as GeoJSON for map display")
        .Produces<object>();

        return endpoints;
    }
}

public record CreateObservationRequest(
    double Longitude,
    double Latitude,
    DateTime ObservationTime,
    string Title,
    ObservationType Type,
    string? Description = null,
    int Severity = 3,
    string? CitizenEmail = null,
    string? CitizenName = null);
