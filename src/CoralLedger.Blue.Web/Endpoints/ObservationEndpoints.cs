using System.Security.Claims;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Common.Models;
using CoralLedger.Blue.Application.Features.Observations.Commands.CreateObservation;
using CoralLedger.Blue.Application.Features.Observations.Queries.GetObservationById;
using CoralLedger.Blue.Application.Features.Observations.Queries.GetObservations;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints;

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
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
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
            var result = await mediator.Send(new GetObservationByIdQuery(id), ct).ConfigureAwait(false);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetObservationById")
        .WithDescription("Get detailed information about a specific observation")
        .Produces<ObservationDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/observations - Create new observation
        group.MapPost("/", async (
            CreateObservationRequest request,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            // Extract authenticated user information from API key claims
            var clientId = user.FindFirst("ClientId")?.Value;
            var authenticatedEmail = user.FindFirst(ClaimTypes.Email)?.Value;
            
            // For citizen observations, use the contact email from API client if available
            // Otherwise use the email from request (will be marked as unverified)
            var citizenEmail = authenticatedEmail ?? request.CitizenEmail;
            
            var command = new CreateObservationCommand(
                request.Longitude,
                request.Latitude,
                request.ObservationTime,
                request.Title,
                request.Type,
                request.Description,
                request.Severity,
                citizenEmail,
                request.CitizenName,
                clientId);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Created($"/api/observations/{result.ObservationId}", result);
        })
        .WithName("CreateObservation")
        .WithDescription("Submit a new citizen science observation. Requires API key authentication.")
        .RequireAuthorization()
        .Produces<CreateObservationResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/observations/{id}/photos - Upload photo for observation
        group.MapPost("/{id:guid}/photos", async (
            Guid id,
            IFormFile file,
            string? caption,
            ClaimsPrincipal user,
            IMarineDbContext dbContext,
            IBlobStorageService blobStorage,
            CancellationToken ct = default) =>
        {
            // Verify observation exists
            var observation = await dbContext.CitizenObservations
                .Include(o => o.Photos)
                .FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);

            if (observation is null)
            {
                return Results.NotFound(new { error = "Observation not found" });
            }

            // Verify the observation belongs to the authenticated client
            var clientId = user.FindFirst("ClientId")?.Value;
            if (observation.ApiClientId != clientId)
            {
                return Results.Forbid();
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
                stream, file.FileName, file.ContentType, ct).ConfigureAwait(false);

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
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Created($"/api/observations/{id}/photos/{photo.Id}", new
            {
                id = photo.Id,
                blobUri = photo.BlobUri,
                caption = photo.Caption,
                displayOrder = photo.DisplayOrder
            });
        })
        .WithName("UploadObservationPhoto")
        .WithDescription("Upload a photo for an observation. Requires API key authentication.")
        .RequireAuthorization()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        // POST /api/observations/{id}/classify-species - AI species classification
        group.MapPost("/{id:guid}/classify-species", async (
            Guid id,
            ClaimsPrincipal user,
            IMarineDbContext dbContext,
            ISpeciesClassificationService classificationService,
            CancellationToken ct = default) =>
        {
            // Verify observation exists and has photos
            var observation = await dbContext.CitizenObservations
                .Include(o => o.Photos)
                .FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);

            if (observation is null)
            {
                return Results.NotFound(new { error = "Observation not found" });
            }

            // Verify the observation belongs to the authenticated client
            var clientId = user.FindFirst("ClientId")?.Value;
            if (observation.ApiClientId != clientId)
            {
                return Results.Forbid();
            }

            if (!observation.Photos.Any())
            {
                return Results.BadRequest(new { error = "Observation has no photos to classify" });
            }

            if (!classificationService.IsConfigured)
            {
                return Results.Problem("AI classification service is not configured", statusCode: 503);
            }

            // Classify each photo and aggregate results
            var allSpecies = new List<IdentifiedSpecies>();
            var errors = new List<string>();

            foreach (var photo in observation.Photos.OrderBy(p => p.DisplayOrder))
            {
                var result = await classificationService.ClassifyPhotoAsync(photo.BlobUri, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    allSpecies.AddRange(result.Species);
                }
                else if (result.Error != null)
                {
                    errors.Add($"Photo {photo.Id}: {result.Error}");
                }
            }

            // Deduplicate species by scientific name, keeping highest confidence
            var uniqueSpecies = allSpecies
                .GroupBy(s => s.ScientificName.ToLower())
                .Select(g => g.OrderByDescending(s => s.ConfidenceScore).First())
                .ToList();

            // Check for priority flags
            var hasInvasive = uniqueSpecies.Any(s => s.IsInvasive);
            var hasConservationConcern = uniqueSpecies.Any(s => s.IsConservationConcern);
            var requiresExpertReview = uniqueSpecies.Any(s => s.RequiresExpertVerification);

            return Results.Ok(new
            {
                observationId = id,
                species = uniqueSpecies,
                summary = new
                {
                    totalSpeciesIdentified = uniqueSpecies.Count,
                    hasInvasiveSpecies = hasInvasive,
                    hasConservationConcern,
                    requiresExpertReview,
                    photosAnalyzed = observation.Photos.Count,
                    errors = errors.Count > 0 ? errors : null
                },
                alerts = new
                {
                    invasive = hasInvasive ? "ALERT: Invasive species detected - consider removal" : null,
                    conservation = hasConservationConcern ? "NOTE: Conservation-concern species detected" : null
                }
            });
        })
        .WithName("ClassifyObservationSpecies")
        .WithDescription("Use AI to identify marine species in observation photos. Requires API key authentication.")
        .RequireAuthorization()
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status503ServiceUnavailable);

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
                .ToListAsync(ct).ConfigureAwait(false);

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

        // POST /api/observations/validate - Pre-validate observation before submission
        // Sprint 4.2 US-4.2.2/4.2.3/4.2.6: Validation with EXIF, geofencing, plausibility
        group.MapPost("/validate", async (
            ValidateObservationRequest request,
            IObservationValidationService validationService,
            CancellationToken ct = default) =>
        {
            var validationRequest = new ObservationValidationRequest
            {
                Longitude = request.Longitude,
                Latitude = request.Latitude,
                ObservationTime = request.ObservationTime,
                ObservationType = request.ObservationType,
                TrustLevel = request.TrustLevel ?? 0,
                DepthMeters = request.DepthMeters,
                Notes = request.Notes,
                Photos = new List<PhotoValidationData>()
            };

            var result = await validationService.ValidateObservationAsync(validationRequest, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                isValid = result.IsValid,
                hasBlockingIssues = result.HasBlockingIssues,
                hasWarnings = result.HasWarnings,
                requiresModerationReview = result.RequiresModerationReview,
                trustScoreAdjustment = result.TrustScoreAdjustment,
                summary = result.Summary,
                geofence = new
                {
                    isWithinBahamasEez = result.GeofenceResult.IsWithinBahamasEez,
                    areCoordinatesValid = result.GeofenceResult.AreCoordinatesValid,
                    errorMessage = result.GeofenceResult.ErrorMessage,
                    distanceToEezKm = result.GeofenceResult.DistanceToEezKm
                },
                plausibilityIssues = result.PlausibilityIssues.Select(p => new
                {
                    checkType = p.CheckType.ToString(),
                    severity = p.Severity.ToString(),
                    description = p.Description,
                    affectedField = p.AffectedField
                })
            });
        })
        .WithName("ValidateObservation")
        .WithDescription("Pre-validate observation data before submission (geofencing, plausibility checks)")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/observations/{id}/photos/{photoId}/validate-exif - Validate photo EXIF against observation location
        // Sprint 4.2 US-4.2.2: EXIF validation with 500m tolerance
        group.MapPost("/{id:guid}/photos/{photoId:guid}/validate-exif", async (
            Guid id,
            Guid photoId,
            IMarineDbContext dbContext,
            IObservationValidationService validationService,
            IBlobStorageService blobStorage,
            CancellationToken ct = default) =>
        {
            // Get observation with photo
            var observation = await dbContext.CitizenObservations
                .Include(o => o.Photos)
                .FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);

            if (observation is null)
            {
                return Results.NotFound(new { error = "Observation not found" });
            }

            var photo = observation.Photos.FirstOrDefault(p => p.Id == photoId);
            if (photo is null)
            {
                return Results.NotFound(new { error = "Photo not found" });
            }

            // Download photo from blob storage to extract EXIF
            var photoStream = await blobStorage.DownloadPhotoAsync(photo.BlobName, ct).ConfigureAwait(false);
            if (photoStream is null)
            {
                return Results.Problem("Failed to download photo for EXIF analysis", statusCode: 500);
            }

            try
            {
                // Extract EXIF GPS data
                var exifGps = await validationService.ExtractExifGpsAsync(photoStream).ConfigureAwait(false);

                if (exifGps is null)
                {
                    return Results.Ok(new
                    {
                        photoId,
                        hasExifGps = false,
                        message = "Photo does not contain GPS metadata"
                    });
                }

                // Validate against observation location
                var validationResult = validationService.ValidateExifLocation(
                    observation.Location.X, // Longitude
                    observation.Location.Y, // Latitude
                    exifGps.Longitude,
                    exifGps.Latitude);

                return Results.Ok(new
                {
                    photoId,
                    hasExifGps = true,
                    isLocationValid = validationResult.IsLocationValid,
                    distanceMeters = validationResult.DistanceMeters,
                    toleranceMeters = validationResult.ToleranceMeters,
                    exifGps = new
                    {
                        longitude = exifGps.Longitude,
                        latitude = exifGps.Latitude,
                        altitude = exifGps.AltitudeMeters,
                        timestamp = exifGps.GpsTimestamp,
                        accuracy = exifGps.AccuracyMeters
                    },
                    observationLocation = new
                    {
                        longitude = observation.Location.X,
                        latitude = observation.Location.Y
                    }
                });
            }
            finally
            {
                await photoStream.DisposeAsync();
            }
        })
        .WithName("ValidatePhotoExif")
        .WithDescription("Validate photo EXIF GPS against observation location (500m tolerance per Dr. Thorne)")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

/// <summary>
/// Request model for observation validation
/// </summary>
public record ValidateObservationRequest(
    double Longitude,
    double Latitude,
    DateTime ObservationTime,
    string ObservationType,
    int? TrustLevel = 0,
    double? DepthMeters = null,
    string? Notes = null);

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
