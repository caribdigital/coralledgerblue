using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Reports.DTOs;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Application.Features.Reports.Queries;

/// <summary>
/// Query to get MPA status report data
/// </summary>
public record GetMpaStatusReportDataQuery(Guid MpaId, DateTime? FromDate = null, DateTime? ToDate = null) : IRequest<MpaStatusReportDto?>;

public class GetMpaStatusReportDataQueryHandler : IRequestHandler<GetMpaStatusReportDataQuery, MpaStatusReportDto?>
{
    private readonly IMarineDbContext _context;

    public GetMpaStatusReportDataQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<MpaStatusReportDto?> Handle(GetMpaStatusReportDataQuery request, CancellationToken cancellationToken)
    {
        var mpa = await _context.MarineProtectedAreas
            .Include(m => m.Reefs)
            .FirstOrDefaultAsync(m => m.Id == request.MpaId, cancellationToken)
            .ConfigureAwait(false);

        if (mpa == null)
            return null;

        var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-3);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        // Get bleaching data
        var bleachingAlerts = await _context.BleachingAlerts
            .Where(b => b.MarineProtectedAreaId == request.MpaId &&
                       b.Date >= DateOnly.FromDateTime(fromDate) &&
                       b.Date <= DateOnly.FromDateTime(toDate))
            .OrderByDescending(b => b.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bleachingData = new BleachingDataSummary
        {
            TotalAlerts = bleachingAlerts.Count,
            MaxDegreeHeatingWeeks = bleachingAlerts.Any() ? bleachingAlerts.Max(b => b.DegreeHeatingWeek) : null,
            AvgSeaSurfaceTemp = bleachingAlerts.Any() ? bleachingAlerts.Average(b => b.SeaSurfaceTemperature) : null,
            MaxSeaSurfaceTemp = bleachingAlerts.Any() ? bleachingAlerts.Max(b => b.SeaSurfaceTemperature) : null,
            CriticalAlertsCount = bleachingAlerts.Count(b => b.DegreeHeatingWeek >= 8), // Critical threshold
            LastAlertDate = bleachingAlerts.Any() ? bleachingAlerts.Max(b => b.Date).ToDateTime(TimeOnly.MinValue) : null,
            RecentAlerts = bleachingAlerts.Take(10).Select(b => new BleachingAlertItem
            {
                Date = b.Date.ToDateTime(TimeOnly.MinValue),
                DegreeHeatingWeeks = b.DegreeHeatingWeek,
                SeaSurfaceTemp = b.SeaSurfaceTemperature,
                AlertLevel = b.DegreeHeatingWeek >= 8 ? "Critical" : b.DegreeHeatingWeek >= 4 ? "Warning" : "Watch"
            }).ToList()
        };

        // Get vessel events
        var vesselEvents = await _context.VesselEvents
            .Include(e => e.Vessel)
            .Where(e => e.MarineProtectedAreaId == request.MpaId &&
                       e.StartTime >= fromDate &&
                       e.StartTime <= toDate)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fishingActivity = new FishingActivitySummary
        {
            TotalVesselEvents = vesselEvents.Count,
            FishingEvents = vesselEvents.Count(e => e.EventType == VesselEventType.Fishing),
            PortVisits = vesselEvents.Count(e => e.EventType == VesselEventType.PortVisit),
            Encounters = vesselEvents.Count(e => e.EventType == VesselEventType.Encounter),
            UniqueVessels = vesselEvents.Select(e => e.VesselId).Distinct().Count(),
            LastActivityDate = vesselEvents.Any() ? vesselEvents.Max(e => e.StartTime) : null,
            RecentEvents = vesselEvents.Take(10).Select(e => new VesselEventItem
            {
                StartTime = e.StartTime,
                EventType = e.EventType.ToString(),
                VesselName = e.Vessel?.Name ?? "Unknown",
                VesselMmsi = e.Vessel?.Mmsi,
                DurationHours = e.EndTime.HasValue ? (e.EndTime.Value - e.StartTime).TotalHours : null
            }).ToList(),
            EventsByType = vesselEvents.GroupBy(e => e.EventType.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Get citizen observations
        var observations = await _context.CitizenObservations
            .Where(o => o.MarineProtectedAreaId == request.MpaId &&
                       o.ObservationTime >= fromDate &&
                       o.ObservationTime <= toDate)
            .OrderByDescending(o => o.ObservationTime)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var observationsSummary = new ObservationsSummary
        {
            TotalObservations = observations.Count,
            ApprovedObservations = observations.Count(o => o.Status == ObservationStatus.Approved),
            PendingObservations = observations.Count(o => o.Status == ObservationStatus.Pending),
            RejectedObservations = observations.Count(o => o.Status == ObservationStatus.Rejected),
            AvgSeverity = observations.Any() ? observations.Average(o => o.Severity) : null,
            LastObservationDate = observations.Any() ? observations.Max(o => o.ObservationTime) : null,
            RecentObservations = observations.Take(10).Select(o => new ObservationItem
            {
                ObservedAt = o.ObservationTime,
                Description = o.Description ?? string.Empty,
                Severity = o.Severity,
                Status = o.Status.ToString(),
                ObserverName = o.CitizenName
            }).ToList(),
            ObservationsBySeverity = observations.GroupBy(o => o.Severity)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return new MpaStatusReportDto
        {
            MpaId = mpa.Id,
            Name = mpa.Name,
            LocalName = mpa.LocalName,
            AreaSquareKm = mpa.AreaSquareKm,
            Status = mpa.Status.ToString(),
            ProtectionLevel = mpa.ProtectionLevel.ToString(),
            IslandGroup = mpa.IslandGroup.ToString(),
            DesignationDate = mpa.DesignationDate,
            ManagingAuthority = mpa.ManagingAuthority,
            Description = mpa.Description,
            CentroidLongitude = mpa.Centroid.X,
            CentroidLatitude = mpa.Centroid.Y,
            ReefCount = mpa.Reefs.Count,
            BleachingData = bleachingData,
            FishingActivity = fishingActivity,
            Observations = observationsSummary,
            GeneratedAt = DateTime.UtcNow,
            DataFromDate = fromDate,
            DataToDate = toDate
        };
    }
}
