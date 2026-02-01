using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.PatrolRoutes.DTOs;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Application.Features.PatrolRoutes.Queries.GetPatrolRoutes;

public record GetPatrolRoutesQuery(
    string? OfficerId = null,
    PatrolRouteStatus? Status = null,
    Guid? MarineProtectedAreaId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Limit = 100
) : IRequest<IReadOnlyList<PatrolRouteSummaryDto>>;

public class GetPatrolRoutesQueryHandler : IRequestHandler<GetPatrolRoutesQuery, IReadOnlyList<PatrolRouteSummaryDto>>
{
    private readonly IMarineDbContext _context;

    public GetPatrolRoutesQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PatrolRouteSummaryDto>> Handle(
        GetPatrolRoutesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.PatrolRoutes
            .AsNoTracking()
            .Include(p => p.Points)
            .Include(p => p.Waypoints)
            .Include(p => p.MarineProtectedArea)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.OfficerId))
            query = query.Where(p => p.OfficerId == request.OfficerId);

        if (request.Status.HasValue)
            query = query.Where(p => p.Status == request.Status.Value);

        if (request.MarineProtectedAreaId.HasValue)
            query = query.Where(p => p.MarineProtectedAreaId == request.MarineProtectedAreaId.Value);

        if (request.FromDate.HasValue)
            query = query.Where(p => p.StartTime >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(p => p.StartTime <= request.ToDate.Value);

        var routes = await query
            .OrderByDescending(p => p.StartTime)
            .Take(request.Limit)
            .Select(p => new PatrolRouteSummaryDto(
                p.Id,
                p.OfficerName,
                p.OfficerId,
                p.StartTime,
                p.EndTime,
                p.Status.ToString(),
                p.TotalDistanceMeters,
                p.DurationSeconds,
                p.Points.Count,
                p.Waypoints.Count,
                p.MarineProtectedArea != null ? p.MarineProtectedArea.Name : null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return routes;
    }
}
