using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Calculates reef health based on multiple data sources following
/// Dr. Bethel's marine science guidelines for Caribbean reef assessment.
///
/// Scoring weights (based on Caribbean coral reef research):
/// - Bleaching stress (NOAA CRW): 35% - primary threat indicator
/// - Coral cover: 30% - structural health indicator
/// - Citizen observations: 20% - ground truth validation
/// - Fishing pressure: 15% - anthropogenic stress factor
/// </summary>
public class ReefHealthCalculator : IReefHealthCalculator
{
    private readonly MarineDbContext _context;
    private readonly ILogger<ReefHealthCalculator> _logger;
    private readonly IDateTimeService _dateTime;

    // Scoring weights (sum = 1.0)
    private const double BleachingWeight = 0.35;
    private const double CoralCoverWeight = 0.30;
    private const double ObservationWeight = 0.20;
    private const double FishingPressureWeight = 0.15;

    // Distance threshold for "nearby" fishing activity (km)
    private const double FishingProximityThresholdKm = 5.0;

    public ReefHealthCalculator(
        MarineDbContext context,
        ILogger<ReefHealthCalculator> logger,
        IDateTimeService dateTime)
    {
        _context = context;
        _logger = logger;
        _dateTime = dateTime;
    }

    public async Task<ReefHealthAssessment> CalculateHealthAsync(
        Guid reefId,
        CancellationToken cancellationToken = default)
    {
        var reef = await _context.Reefs
            .Include(r => r.MarineProtectedArea)
            .FirstOrDefaultAsync(r => r.Id == reefId, cancellationToken).ConfigureAwait(false);

        if (reef == null)
        {
            throw new ArgumentException($"Reef with ID {reefId} not found", nameof(reefId));
        }

        var metrics = await GatherMetricsAsync(reef.Id, reef.Location, cancellationToken).ConfigureAwait(false);
        var assessment = CalculateFromMetrics(metrics);

        return assessment with
        {
            ReefId = reef.Id,
            ReefName = reef.Name
        };
    }

    public async Task<IEnumerable<ReefHealthAssessment>> CalculateMpaReefHealthAsync(
        Guid mpaId,
        CancellationToken cancellationToken = default)
    {
        var reefs = await _context.Reefs
            .Where(r => r.MarineProtectedAreaId == mpaId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var assessments = new List<ReefHealthAssessment>();

        foreach (var reef in reefs)
        {
            try
            {
                var assessment = await CalculateHealthAsync(reef.Id, cancellationToken).ConfigureAwait(false);
                assessments.Add(assessment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate health for reef {ReefId}", reef.Id);
            }
        }

        return assessments;
    }

    public ReefHealthAssessment CalculateFromMetrics(ReefHealthMetrics metrics)
    {
        var now = _dateTime.UtcNow;
        var alerts = new List<string>();
        var recommendations = new List<string>();

        // Calculate component scores (0-100, higher is better)
        var bleachingScore = CalculateBleachingScore(metrics, alerts, recommendations);
        var coralCoverScore = CalculateCoralCoverScore(metrics, alerts, recommendations);
        var observationScore = CalculateObservationScore(metrics, alerts);
        var fishingPressureScore = CalculateFishingPressureScore(metrics, alerts, recommendations);

        // Calculate weighted overall score
        var overallScore =
            (bleachingScore * BleachingWeight) +
            (coralCoverScore * CoralCoverWeight) +
            (observationScore * ObservationWeight) +
            (fishingPressureScore * FishingPressureWeight);

        // Determine health status from score
        var healthStatus = DetermineHealthStatus(overallScore, metrics);

        // Data freshness checks
        var hasRecentSurvey = metrics.LastSurveyDate.HasValue &&
            metrics.LastSurveyDate.Value.ToDateTime(TimeOnly.MinValue) > now.AddMonths(-6);
        var hasRecentBleaching = metrics.DegreeHeatingWeek.HasValue || metrics.BleachingAlertLevel.HasValue;
        var hasCitizenReports = metrics.RecentObservationCount > 0;

        if (!hasRecentSurvey)
        {
            recommendations.Add("Schedule reef survey - last survey data is over 6 months old");
        }

        return new ReefHealthAssessment
        {
            ReefId = Guid.Empty, // Set by caller
            ReefName = string.Empty, // Set by caller
            HealthStatus = healthStatus,
            OverallScore = Math.Round(overallScore, 1),
            AssessmentTime = now,
            BleachingScore = Math.Round(bleachingScore, 1),
            CoralCoverScore = Math.Round(coralCoverScore, 1),
            ObservationScore = Math.Round(observationScore, 1),
            FishingPressureScore = Math.Round(fishingPressureScore, 1),
            HasRecentSurvey = hasRecentSurvey,
            HasRecentBleachingData = hasRecentBleaching,
            HasCitizenReports = hasCitizenReports,
            Alerts = alerts.AsReadOnly(),
            Recommendations = recommendations.AsReadOnly()
        };
    }

    private async Task<ReefHealthMetrics> GatherMetricsAsync(
        Guid reefId,
        Geometry reefLocation,
        CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sevenDaysAgo = now.AddDays(-7);

        // Get reef survey data
        var reef = await _context.Reefs
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reefId, cancellationToken).ConfigureAwait(false);

        // Get latest bleaching data for reef location
        var latestBleaching = await _context.BleachingAlerts
            .Where(b => b.ReefId == reefId ||
                        b.Location.Distance(reefLocation) < 5000) // Within 5km
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // Get citizen observations near reef
        var observations = await _context.CitizenObservations
            .Where(o => o.ReefId == reefId ||
                        o.Location.Distance(reefLocation) < 2000) // Within 2km
            .Where(o => o.CreatedAt >= thirtyDaysAgo)
            .Where(o => o.Status == ObservationStatus.Approved)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Get fishing events near reef
        var fishingEvents = await _context.VesselEvents
            .Where(e => e.EventType == VesselEventType.Fishing)
            .Where(e => e.Location.Distance(reefLocation) < FishingProximityThresholdKm * 1000)
            .Where(e => e.StartTime >= thirtyDaysAgo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var nearestFishing = await _context.VesselEvents
            .Where(e => e.EventType == VesselEventType.Fishing)
            .Where(e => e.StartTime >= sevenDaysAgo)
            .OrderBy(e => e.Location.Distance(reefLocation))
            .Select(e => e.Location.Distance(reefLocation))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return new ReefHealthMetrics
        {
            // NOAA data
            DegreeHeatingWeek = latestBleaching?.DegreeHeatingWeek,
            SstAnomaly = latestBleaching?.SstAnomaly,
            BleachingAlertLevel = latestBleaching?.AlertLevel,

            // Survey data
            CoralCoverPercentage = reef?.CoralCoverPercentage,
            BleachingPercentage = reef?.BleachingPercentage,
            LastSurveyDate = reef?.LastSurveyDate,

            // Citizen observations
            RecentObservationCount = observations.Count,
            AverageSeverity = observations.Count > 0
                ? observations.Average(o => o.Severity)
                : null,
            BleachingReportCount = observations
                .Count(o => o.Type == ObservationType.CoralBleaching),
            DebrisReportCount = observations
                .Count(o => o.Type == ObservationType.MarineDebris),

            // Fishing pressure
            FishingEventsNearby7Days = fishingEvents
                .Count(e => e.StartTime >= sevenDaysAgo),
            FishingEventsNearby30Days = fishingEvents.Count,
            NearestFishingDistanceKm = nearestFishing > 0
                ? nearestFishing / 1000.0
                : double.MaxValue
        };
    }

    /// <summary>
    /// Calculate bleaching score based on NOAA CRW data and survey bleaching percentage.
    /// DHW thresholds based on NOAA CRW Alert Level criteria.
    /// </summary>
    private static double CalculateBleachingScore(
        ReefHealthMetrics metrics,
        List<string> alerts,
        List<string> recommendations)
    {
        var score = 100.0;

        // DHW-based scoring (primary bleaching stress indicator)
        if (metrics.DegreeHeatingWeek.HasValue)
        {
            var dhw = metrics.DegreeHeatingWeek.Value;

            if (dhw >= 16)
            {
                score -= 60; // Severe bleaching expected
                alerts.Add($"CRITICAL: DHW at {dhw:F1}°C-weeks - mass mortality likely");
            }
            else if (dhw >= 8)
            {
                score -= 40; // Significant bleaching expected
                alerts.Add($"WARNING: DHW at {dhw:F1}°C-weeks - significant bleaching likely");
            }
            else if (dhw >= 4)
            {
                score -= 20; // Bleaching possible
                alerts.Add($"WATCH: DHW at {dhw:F1}°C-weeks - bleaching stress building");
            }
            else if (dhw > 0)
            {
                score -= 5; // Minor stress
            }
        }

        // SST anomaly scoring (temperature stress indicator)
        if (metrics.SstAnomaly.HasValue)
        {
            var anomaly = metrics.SstAnomaly.Value;

            if (anomaly > 2.0)
            {
                score -= 15;
                alerts.Add($"High SST anomaly: +{anomaly:F1}°C above normal");
            }
            else if (anomaly > 1.0)
            {
                score -= 8;
            }
            else if (anomaly > 0.5)
            {
                score -= 3;
            }
        }

        // Survey bleaching percentage
        if (metrics.BleachingPercentage.HasValue)
        {
            var bleaching = metrics.BleachingPercentage.Value;

            if (bleaching >= 50)
            {
                score -= 30;
                alerts.Add($"Survey shows {bleaching:F0}% coral bleaching");
                recommendations.Add("Initiate emergency coral restoration protocols");
            }
            else if (bleaching >= 25)
            {
                score -= 20;
                recommendations.Add("Monitor bleaching progression weekly");
            }
            else if (bleaching >= 10)
            {
                score -= 10;
            }
        }

        return Math.Max(0, score);
    }

    /// <summary>
    /// Calculate coral cover score based on Caribbean reef benchmarks.
    /// Reference: Healthy Caribbean reefs typically have 20-40% coral cover.
    /// </summary>
    private static double CalculateCoralCoverScore(
        ReefHealthMetrics metrics,
        List<string> alerts,
        List<string> recommendations)
    {
        if (!metrics.CoralCoverPercentage.HasValue)
        {
            // No data - return neutral score
            return 50.0;
        }

        var coralCover = metrics.CoralCoverPercentage.Value;

        // Scoring based on Caribbean reef benchmarks
        double score;
        if (coralCover >= 40)
        {
            score = 100; // Excellent coverage
        }
        else if (coralCover >= 25)
        {
            score = 85; // Good coverage
        }
        else if (coralCover >= 15)
        {
            score = 65; // Fair coverage
        }
        else if (coralCover >= 5)
        {
            score = 40; // Poor coverage
            recommendations.Add("Consider coral restoration program");
        }
        else
        {
            score = 20; // Critical - very low coverage
            alerts.Add($"Critical coral cover: only {coralCover:F1}%");
            recommendations.Add("Urgent coral restoration needed");
        }

        return score;
    }

    /// <summary>
    /// Calculate observation score based on citizen reports.
    /// Negative reports (high severity, bleaching, debris) lower the score.
    /// </summary>
    private static double CalculateObservationScore(
        ReefHealthMetrics metrics,
        List<string> alerts)
    {
        if (metrics.RecentObservationCount == 0)
        {
            // No recent observations - return neutral score
            return 50.0;
        }

        var score = 100.0;

        // Bleaching reports impact
        if (metrics.BleachingReportCount > 0)
        {
            score -= Math.Min(30, metrics.BleachingReportCount * 10);
            if (metrics.BleachingReportCount >= 3)
            {
                alerts.Add($"{metrics.BleachingReportCount} citizen bleaching reports in last 30 days");
            }
        }

        // Debris reports impact
        if (metrics.DebrisReportCount > 0)
        {
            score -= Math.Min(15, metrics.DebrisReportCount * 5);
        }

        // Average severity impact (1-5 scale, 3 is neutral)
        if (metrics.AverageSeverity.HasValue)
        {
            var severityImpact = (metrics.AverageSeverity.Value - 3) * 10;
            score -= severityImpact; // High severity reduces score
        }

        return Math.Clamp(score, 0, 100);
    }

    /// <summary>
    /// Calculate fishing pressure score based on nearby fishing activity.
    /// More fishing activity = lower score (higher pressure).
    /// </summary>
    private static double CalculateFishingPressureScore(
        ReefHealthMetrics metrics,
        List<string> alerts,
        List<string> recommendations)
    {
        var score = 100.0;

        // 7-day fishing events impact (more weight for recent activity)
        if (metrics.FishingEventsNearby7Days > 0)
        {
            score -= Math.Min(40, metrics.FishingEventsNearby7Days * 8);

            if (metrics.FishingEventsNearby7Days >= 5)
            {
                alerts.Add($"High fishing pressure: {metrics.FishingEventsNearby7Days} events in last 7 days");
                recommendations.Add("Review fishing regulations enforcement");
            }
        }

        // 30-day fishing events impact
        var olderEvents = metrics.FishingEventsNearby30Days - metrics.FishingEventsNearby7Days;
        if (olderEvents > 0)
        {
            score -= Math.Min(20, olderEvents * 2);
        }

        // Proximity impact - fishing very close to reef is worse
        if (metrics.NearestFishingDistanceKm < 1.0)
        {
            score -= 15;
            alerts.Add($"Fishing activity detected {metrics.NearestFishingDistanceKm:F1}km from reef");
        }
        else if (metrics.NearestFishingDistanceKm < 2.0)
        {
            score -= 8;
        }

        return Math.Max(0, score);
    }

    /// <summary>
    /// Determine health status from overall score with contextual adjustments.
    /// </summary>
    private static ReefHealth DetermineHealthStatus(double score, ReefHealthMetrics metrics)
    {
        // Critical override: DHW >= 16 = always Critical
        if (metrics.DegreeHeatingWeek >= 16)
        {
            return ReefHealth.Critical;
        }

        // Critical override: Coral cover < 5% = always Critical
        if (metrics.CoralCoverPercentage < 5)
        {
            return ReefHealth.Critical;
        }

        // Score-based determination
        return score switch
        {
            >= 85 => ReefHealth.Excellent,
            >= 70 => ReefHealth.Good,
            >= 50 => ReefHealth.Fair,
            >= 30 => ReefHealth.Poor,
            _ => ReefHealth.Critical
        };
    }
}
