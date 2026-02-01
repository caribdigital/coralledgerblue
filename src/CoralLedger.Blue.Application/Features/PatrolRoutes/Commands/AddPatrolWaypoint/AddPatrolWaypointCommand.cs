using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.AddPatrolWaypoint;

public record AddPatrolWaypointCommand(
    Guid PatrolRouteId,
    double Longitude,
    double Latitude,
    string Title,
    string? Notes = null,
    string? WaypointType = null,
    DateTime? Timestamp = null
) : IRequest<AddPatrolWaypointResult>;

public record AddPatrolWaypointResult(
    bool Success,
    Guid? WaypointId = null,
    string? Error = null);

public class AddPatrolWaypointCommandHandler : IRequestHandler<AddPatrolWaypointCommand, AddPatrolWaypointResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<AddPatrolWaypointCommandHandler> _logger;

    public AddPatrolWaypointCommandHandler(
        IMarineDbContext context,
        ILogger<AddPatrolWaypointCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AddPatrolWaypointResult> Handle(
        AddPatrolWaypointCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var patrolRoute = await _context.PatrolRoutes
                .FirstOrDefaultAsync(p => p.Id == request.PatrolRouteId, cancellationToken)
                .ConfigureAwait(false);

            if (patrolRoute == null)
            {
                return new AddPatrolWaypointResult(false, Error: "Patrol route not found");
            }

            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var location = factory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

            var waypoint = PatrolWaypoint.Create(
                request.PatrolRouteId,
                location,
                request.Timestamp ?? DateTime.UtcNow,
                request.Title,
                request.Notes,
                request.WaypointType);

            patrolRoute.AddWaypoint(waypoint);
            _context.PatrolWaypoints.Add(waypoint);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Added waypoint '{Title}' to patrol route {RouteId}",
                request.Title, request.PatrolRouteId);

            return new AddPatrolWaypointResult(
                Success: true,
                WaypointId: waypoint.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot add waypoint to patrol route {RouteId}", request.PatrolRouteId);
            return new AddPatrolWaypointResult(false, Error: ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add waypoint to patrol route {RouteId}", request.PatrolRouteId);
            return new AddPatrolWaypointResult(false, Error: ex.Message);
        }
    }
}
