using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Features.Observations.Queries.GetObservations;

public record GetObservationsQuery(
    ObservationType? Type = null,
    ObservationStatus? Status = null,
    Guid? MpaId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Limit = 100
) : IRequest<IReadOnlyList<ObservationSummaryDto>>;

public record ObservationSummaryDto(
    Guid Id,
    double Longitude,
    double Latitude,
    DateTime ObservationTime,
    string Title,
    ObservationType Type,
    int Severity,
    ObservationStatus Status,
    bool? IsInMpa,
    string? MpaName,
    string? CitizenName,
    int PhotoCount,
    DateTime CreatedAt);

public class GetObservationsQueryHandler : IRequestHandler<GetObservationsQuery, IReadOnlyList<ObservationSummaryDto>>
{
    private readonly IMarineDbContext _context;

    public GetObservationsQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ObservationSummaryDto>> Handle(
        GetObservationsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.CitizenObservations
            .AsNoTracking()
            .Include(o => o.MarineProtectedArea)
            .Include(o => o.Photos)
            .AsQueryable();

        if (request.Type.HasValue)
            query = query.Where(o => o.Type == request.Type.Value);

        if (request.Status.HasValue)
            query = query.Where(o => o.Status == request.Status.Value);

        if (request.MpaId.HasValue)
            query = query.Where(o => o.MarineProtectedAreaId == request.MpaId.Value);

        if (request.FromDate.HasValue)
            query = query.Where(o => o.ObservationTime >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(o => o.ObservationTime <= request.ToDate.Value);

        var observations = await query
            .OrderByDescending(o => o.ObservationTime)
            .Take(request.Limit)
            .Select(o => new ObservationSummaryDto(
                o.Id,
                o.Location.X,
                o.Location.Y,
                o.ObservationTime,
                o.Title,
                o.Type,
                o.Severity,
                o.Status,
                o.IsInMpa,
                o.MarineProtectedArea != null ? o.MarineProtectedArea.Name : null,
                o.CitizenName,
                o.Photos.Count,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return observations;
    }
}
