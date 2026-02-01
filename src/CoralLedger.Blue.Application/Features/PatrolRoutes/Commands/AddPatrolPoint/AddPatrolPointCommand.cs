using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.AddPatrolPoint;

public record AddPatrolPointCommand(
    Guid PatrolRouteId,
    double Longitude,
    double Latitude,
    DateTime? Timestamp = null,
    double? Accuracy = null,
    double? Altitude = null,
    double? Speed = null,
    double? Heading = null
) : IRequest<AddPatrolPointResult>;

public record AddPatrolPointResult(
    bool Success,
    Guid? PointId = null,
    string? Error = null);

public class AddPatrolPointCommandHandler : IRequestHandler<AddPatrolPointCommand, AddPatrolPointResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<AddPatrolPointCommandHandler> _logger;

    public AddPatrolPointCommandHandler(
        IMarineDbContext context,
        ILogger<AddPatrolPointCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AddPatrolPointResult> Handle(
        AddPatrolPointCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var patrolRoute = await _context.PatrolRoutes
                .FirstOrDefaultAsync(p => p.Id == request.PatrolRouteId, cancellationToken)
                .ConfigureAwait(false);

            if (patrolRoute == null)
            {
                return new AddPatrolPointResult(false, Error: "Patrol route not found");
            }

            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var location = factory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

            var point = PatrolRoutePoint.Create(
                request.PatrolRouteId,
                location,
                request.Timestamp ?? DateTime.UtcNow,
                request.Accuracy,
                request.Altitude,
                request.Speed,
                request.Heading);

            patrolRoute.AddPoint(point);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Added GPS point {Id} to patrol route {RouteId}",
                point.Id, request.PatrolRouteId);

            return new AddPatrolPointResult(
                Success: true,
                PointId: point.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot add point to patrol route {RouteId}", request.PatrolRouteId);
            return new AddPatrolPointResult(false, Error: ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add point to patrol route {RouteId}", request.PatrolRouteId);
            return new AddPatrolPointResult(false, Error: ex.Message);
        }
    }
}
