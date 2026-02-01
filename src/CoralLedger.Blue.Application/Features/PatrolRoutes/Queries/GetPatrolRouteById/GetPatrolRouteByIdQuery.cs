using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.PatrolRoutes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Application.Features.PatrolRoutes.Queries.GetPatrolRouteById;

public record GetPatrolRouteByIdQuery(Guid Id) : IRequest<PatrolRouteDetailDto?>;

public class GetPatrolRouteByIdQueryHandler : IRequestHandler<GetPatrolRouteByIdQuery, PatrolRouteDetailDto?>
{
    private readonly IMarineDbContext _context;

    public GetPatrolRouteByIdQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<PatrolRouteDetailDto?> Handle(
        GetPatrolRouteByIdQuery request,
        CancellationToken cancellationToken)
    {
        var route = await _context.PatrolRoutes
            .AsNoTracking()
            .Include(p => p.Points)
            .Include(p => p.Waypoints)
            .Include(p => p.MarineProtectedArea)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (route == null)
            return null;

        var points = route.Points
            .OrderBy(p => p.Timestamp)
            .Select(p => new PatrolRoutePointDto(
                p.Id,
                p.Location.X,
                p.Location.Y,
                p.Timestamp,
                p.Accuracy,
                p.Altitude,
                p.Speed,
                p.Heading))
            .ToList();

        var waypoints = route.Waypoints
            .OrderBy(w => w.Timestamp)
            .Select(w => new PatrolWaypointDto(
                w.Id,
                w.Location.X,
                w.Location.Y,
                w.Timestamp,
                w.Title,
                w.Notes,
                w.WaypointType))
            .ToList();

        return new PatrolRouteDetailDto(
            route.Id,
            route.OfficerName,
            route.OfficerId,
            route.StartTime,
            route.EndTime,
            route.Status.ToString(),
            route.Notes,
            route.RecordingIntervalSeconds,
            route.TotalDistanceMeters,
            route.DurationSeconds,
            route.MarineProtectedAreaId,
            route.MarineProtectedArea?.Name,
            points,
            waypoints);
    }
}
