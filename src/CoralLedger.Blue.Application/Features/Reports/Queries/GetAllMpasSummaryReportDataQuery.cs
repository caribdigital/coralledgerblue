using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Reports.DTOs;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Application.Features.Reports.Queries;

/// <summary>
/// Query to get all-MPAs summary report data
/// </summary>
public record GetAllMpasSummaryReportDataQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? IslandGroup = null,
    string? ProtectionLevel = null,
    List<Guid>? MpaIds = null) : IRequest<AllMpasSummaryReportDto>;

public class GetAllMpasSummaryReportDataQueryHandler : IRequestHandler<GetAllMpasSummaryReportDataQuery, AllMpasSummaryReportDto>
{
    private readonly IMarineDbContext _context;

    public GetAllMpasSummaryReportDataQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<AllMpasSummaryReportDto> Handle(GetAllMpasSummaryReportDataQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-3);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        // Build MPA query with filters
        var mpaQuery = _context.MarineProtectedAreas.AsQueryable();

        if (!string.IsNullOrEmpty(request.IslandGroup) && Enum.TryParse<IslandGroup>(request.IslandGroup, true, out var islandGroup))
            mpaQuery = mpaQuery.Where(m => m.IslandGroup == islandGroup);

        if (!string.IsNullOrEmpty(request.ProtectionLevel) && Enum.TryParse<ProtectionLevel>(request.ProtectionLevel, true, out var level))
            mpaQuery = mpaQuery.Where(m => m.ProtectionLevel == level);

        if (request.MpaIds?.Count > 0)
            mpaQuery = mpaQuery.Where(m => request.MpaIds.Contains(m.Id));

        var mpas = await mpaQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var mpaIds = mpas.Select(m => m.Id).ToList();

        // Get aggregated data for all MPAs
        var bleachingAlerts = await _context.BleachingAlerts
            .Where(b => b.MarineProtectedAreaId.HasValue &&
                       mpaIds.Contains(b.MarineProtectedAreaId.Value) &&
                       b.Date >= DateOnly.FromDateTime(fromDate) &&
                       b.Date <= DateOnly.FromDateTime(toDate))
            .GroupBy(b => b.MarineProtectedAreaId!.Value)
            .Select(g => new { MpaId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vesselEvents = await _context.VesselEvents
            .Where(e => e.MarineProtectedAreaId.HasValue &&
                       mpaIds.Contains(e.MarineProtectedAreaId.Value) &&
                       e.StartTime >= fromDate &&
                       e.StartTime <= toDate)
            .GroupBy(e => e.MarineProtectedAreaId!.Value)
            .Select(g => new { MpaId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var observations = await _context.CitizenObservations
            .Where(o => o.MarineProtectedAreaId.HasValue &&
                       mpaIds.Contains(o.MarineProtectedAreaId.Value) &&
                       o.ObservationTime >= fromDate &&
                       o.ObservationTime <= toDate)
            .GroupBy(o => o.MarineProtectedAreaId!.Value)
            .Select(g => new { MpaId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Create MPA summary items
        var mpaSummaries = mpas.Select(m => new MpaSummaryItem
        {
            MpaId = m.Id,
            Name = m.Name,
            IslandGroup = m.IslandGroup.ToString(),
            ProtectionLevel = m.ProtectionLevel.ToString(),
            AreaSquareKm = m.AreaSquareKm,
            TotalAlerts = bleachingAlerts.FirstOrDefault(b => b.MpaId == m.Id)?.Count ?? 0,
            TotalVesselEvents = vesselEvents.FirstOrDefault(v => v.MpaId == m.Id)?.Count ?? 0,
            TotalObservations = observations.FirstOrDefault(o => o.MpaId == m.Id)?.Count ?? 0,
            Status = m.Status.ToString()
        }).ToList();

        // Calculate overall statistics
        var statistics = new OverallStatistics
        {
            TotalBleachingAlerts = bleachingAlerts.Sum(b => b.Count),
            TotalVesselEvents = vesselEvents.Sum(v => v.Count),
            TotalObservations = observations.Sum(o => o.Count),
            ActiveMpas = mpas.Count(m => m.Status == MpaStatus.Active),
            DecommissionedMpas = mpas.Count(m => m.Status == MpaStatus.Decommissioned),
            MpasByIslandGroup = mpas.GroupBy(m => m.IslandGroup.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            MpasByProtectionLevel = mpas.GroupBy(m => m.ProtectionLevel.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return new AllMpasSummaryReportDto
        {
            TotalMpas = mpas.Count,
            TotalAreaSquareKm = mpas.Sum(m => m.AreaSquareKm),
            Mpas = mpaSummaries,
            Statistics = statistics,
            GeneratedAt = DateTime.UtcNow,
            DataFromDate = fromDate,
            DataToDate = toDate
        };
    }
}
