using CoralLedger.Blue.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Application.Features.Bleaching.Queries.GetMpaBleachingHistory;

/// <summary>
/// Query to fetch historical bleaching data for a specific MPA.
/// Returns the last N days of data for trend visualization.
/// </summary>
public record GetMpaBleachingHistoryQuery(Guid MpaId, int Days = 30)
    : IRequest<IReadOnlyList<BleachingHistoryDto>>;

public record BleachingHistoryDto
{
    public DateOnly Date { get; init; }
    public double DegreeHeatingWeek { get; init; }
    public double SeaSurfaceTemperature { get; init; }
    public double SstAnomaly { get; init; }
    public int AlertLevel { get; init; }
}

public class GetMpaBleachingHistoryQueryHandler
    : IRequestHandler<GetMpaBleachingHistoryQuery, IReadOnlyList<BleachingHistoryDto>>
{
    private readonly IMarineDbContext _context;

    public GetMpaBleachingHistoryQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BleachingHistoryDto>> Handle(
        GetMpaBleachingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-request.Days));

        var history = await _context.BleachingAlerts
            .Where(a => a.MarineProtectedAreaId == request.MpaId && a.Date >= cutoffDate)
            .OrderBy(a => a.Date)
            .Select(a => new BleachingHistoryDto
            {
                Date = a.Date,
                DegreeHeatingWeek = a.DegreeHeatingWeek,
                SeaSurfaceTemperature = a.SeaSurfaceTemperature,
                SstAnomaly = a.SstAnomaly,
                AlertLevel = (int)a.AlertLevel
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return history;
    }
}
