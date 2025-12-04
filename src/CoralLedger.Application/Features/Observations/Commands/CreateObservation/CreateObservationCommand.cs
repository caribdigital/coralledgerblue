using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CoralLedger.Application.Features.Observations.Commands.CreateObservation;

public record CreateObservationCommand(
    double Longitude,
    double Latitude,
    DateTime ObservationTime,
    string Title,
    ObservationType Type,
    string? Description = null,
    int Severity = 3,
    string? CitizenEmail = null,
    string? CitizenName = null
) : IRequest<CreateObservationResult>;

public record CreateObservationResult(
    bool Success,
    Guid? ObservationId = null,
    string? MpaName = null,
    string? Error = null);

public class CreateObservationCommandHandler : IRequestHandler<CreateObservationCommand, CreateObservationResult>
{
    private readonly IMarineDbContext _context;
    private readonly ISpatialValidationService _spatialValidation;
    private readonly ILogger<CreateObservationCommandHandler> _logger;

    public CreateObservationCommandHandler(
        IMarineDbContext context,
        ISpatialValidationService spatialValidation,
        ILogger<CreateObservationCommandHandler> logger)
    {
        _context = context;
        _spatialValidation = spatialValidation;
        _logger = logger;
    }

    public async Task<CreateObservationResult> Handle(
        CreateObservationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create point geometry
            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var location = factory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

            // Validate location is within Bahamas
            if (!_spatialValidation.IsWithinBahamas(location))
            {
                return new CreateObservationResult(false, Error: "Location must be within Bahamas territorial waters");
            }

            // Create observation
            var observation = CitizenObservation.Create(
                location,
                request.ObservationTime,
                request.Title,
                request.Type,
                request.Description,
                request.Severity,
                request.CitizenEmail,
                request.CitizenName);

            // Check if observation is within any MPA
            var containingMpa = await _context.MarineProtectedAreas
                .Where(mpa => mpa.Boundary != null && mpa.Boundary.Contains(location))
                .FirstOrDefaultAsync(cancellationToken);

            if (containingMpa != null)
            {
                observation.SetMpaContext(true, containingMpa.Id);
            }
            else
            {
                observation.SetMpaContext(false);
            }

            _context.CitizenObservations.Add(observation);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created citizen observation {Id} of type {Type} at ({Lon}, {Lat})",
                observation.Id, request.Type, request.Longitude, request.Latitude);

            return new CreateObservationResult(
                Success: true,
                ObservationId: observation.Id,
                MpaName: containingMpa?.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create citizen observation");
            return new CreateObservationResult(false, Error: ex.Message);
        }
    }
}
