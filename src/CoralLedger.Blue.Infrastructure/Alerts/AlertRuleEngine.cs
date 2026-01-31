using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Infrastructure.Alerts;

public class AlertRuleEngine : IAlertRuleEngine
{
    private readonly IMarineDbContext _context;
    private readonly IAlertNotificationService _notificationService;
    private readonly ILogger<AlertRuleEngine> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AlertRuleEngine(
        IMarineDbContext context,
        IAlertNotificationService notificationService,
        ILogger<AlertRuleEngine> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Alert>> EvaluateAllRulesAsync(CancellationToken cancellationToken = default)
    {
        var activeRules = await _context.AlertRules
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var allAlerts = new List<Alert>();

        foreach (var rule in activeRules)
        {
            try
            {
                // Check cooldown period
                if (rule.LastTriggeredAt.HasValue &&
                    DateTime.UtcNow - rule.LastTriggeredAt.Value < rule.CooldownPeriod)
                {
                    continue;
                }

                var alerts = await EvaluateRuleInternalAsync(rule, cancellationToken).ConfigureAwait(false);
                allAlerts.AddRange(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule {RuleId}: {RuleName}", rule.Id, rule.Name);
            }
        }

        return allAlerts;
    }

    public async Task<IReadOnlyList<Alert>> EvaluateRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await _context.AlertRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken).ConfigureAwait(false);

        if (rule == null)
        {
            _logger.LogWarning("Rule {RuleId} not found", ruleId);
            return Array.Empty<Alert>();
        }

        return await EvaluateRuleInternalAsync(rule, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Alert>> EvaluateRulesByTypeAsync(AlertType type, CancellationToken cancellationToken = default)
    {
        var rules = await _context.AlertRules
            .Where(r => r.IsActive && r.Type == type)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var allAlerts = new List<Alert>();

        foreach (var rule in rules)
        {
            try
            {
                if (rule.LastTriggeredAt.HasValue &&
                    DateTime.UtcNow - rule.LastTriggeredAt.Value < rule.CooldownPeriod)
                {
                    continue;
                }

                var alerts = await EvaluateRuleInternalAsync(rule, cancellationToken).ConfigureAwait(false);
                allAlerts.AddRange(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule {RuleId}", rule.Id);
            }
        }

        return allAlerts;
    }

    public async Task<int> PersistAlertsAsync(IEnumerable<Alert> alerts, CancellationToken cancellationToken = default)
    {
        var alertList = alerts.ToList();
        if (alertList.Count == 0) return 0;

        _context.Alerts.AddRange(alertList);

        // Update last triggered time for rules
        var ruleIds = alertList.Select(a => a.AlertRuleId).Distinct().ToList();
        var rules = await _context.AlertRules
            .Where(r => ruleIds.Contains(r.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var rule in rules)
        {
            rule.RecordTrigger();
        }

        var count = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send notifications
        foreach (var alert in alertList)
        {
            var rule = rules.FirstOrDefault(r => r.Id == alert.AlertRuleId);
            if (rule != null)
            {
                await _notificationService.SendNotificationAsync(alert, rule, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Persisted {Count} alerts", alertList.Count);
        return alertList.Count;
    }

    private async Task<IReadOnlyList<Alert>> EvaluateRuleInternalAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        return rule.Type switch
        {
            AlertType.Bleaching => await EvaluateBleachingRuleAsync(rule, cancellationToken),
            AlertType.FishingActivity => await EvaluateFishingRuleAsync(rule, cancellationToken),
            AlertType.VesselInMPA => await EvaluateVesselInMpaRuleAsync(rule, cancellationToken),
            AlertType.DegreeHeatingWeek => await EvaluateDhwRuleAsync(rule, cancellationToken),
            AlertType.CitizenObservation => await EvaluateCitizenObservationRuleAsync(rule, cancellationToken),
            _ => Array.Empty<Alert>()
        };
    }

    private async Task<IReadOnlyList<Alert>> EvaluateBleachingRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        var condition = JsonSerializer.Deserialize<BleachingCondition>(rule.Conditions, JsonOptions);
        if (condition == null) return Array.Empty<Alert>();

        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var query = _context.BleachingAlerts
            .Include(b => b.MarineProtectedArea)
            .Where(b => b.Date >= cutoffDate)
            .Where(b => (int)b.AlertLevel >= condition.MinAlertLevel);

        if (rule.MarineProtectedAreaId.HasValue)
        {
            query = query.Where(b => b.MarineProtectedAreaId == rule.MarineProtectedAreaId);
        }

        if (condition.MinDegreeHeatingWeek.HasValue)
        {
            query = query.Where(b => b.DegreeHeatingWeek >= condition.MinDegreeHeatingWeek.Value);
        }

        if (condition.MinSstAnomaly.HasValue)
        {
            query = query.Where(b => b.SstAnomaly >= condition.MinSstAnomaly.Value);
        }

        var bleachingAlerts = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return bleachingAlerts.Select(b => new Alert
        {
            AlertRuleId = rule.Id,
            Type = AlertType.Bleaching,
            Severity = rule.Severity,
            Title = $"Bleaching Alert: {b.AlertLevel} at {b.MarineProtectedArea?.Name ?? "Unknown MPA"}",
            Message = $"Alert Level: {b.AlertLevel}, DHW: {b.DegreeHeatingWeek:F1}°C-weeks, SST Anomaly: {b.SstAnomaly:F1}°C",
            Location = b.Location,
            MarineProtectedAreaId = b.MarineProtectedAreaId,
            Data = JsonSerializer.Serialize(new { b.AlertLevel, b.DegreeHeatingWeek, b.SstAnomaly, b.SeaSurfaceTemperature }),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }).ToList();
    }

    private async Task<IReadOnlyList<Alert>> EvaluateFishingRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        var condition = JsonSerializer.Deserialize<FishingActivityCondition>(rule.Conditions, JsonOptions);
        if (condition == null) return Array.Empty<Alert>();

        var cutoffTime = DateTime.UtcNow.AddHours(-condition.TimeWindowHours);

        var query = _context.VesselEvents
            .Include(e => e.Vessel)
            .Where(e => e.StartTime >= cutoffTime)
            .Where(e => e.EventType == VesselEventType.Fishing);

        if (rule.MarineProtectedAreaId.HasValue)
        {
            query = query.Where(e => e.MarineProtectedAreaId == rule.MarineProtectedAreaId);
        }
        else if (condition.OnlyInsideMpa)
        {
            query = query.Where(e => e.MarineProtectedAreaId != null);
        }

        var events = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (events.Count < condition.MinEventCount)
        {
            return Array.Empty<Alert>();
        }

        // Group by MPA for better alerts
        var groupedByMpa = events
            .GroupBy(e => e.MarineProtectedAreaId)
            .Where(g => g.Count() >= condition.MinEventCount);

        return groupedByMpa.Select(g => new Alert
        {
            AlertRuleId = rule.Id,
            Type = AlertType.FishingActivity,
            Severity = rule.Severity,
            Title = $"High Fishing Activity Detected",
            Message = $"{g.Count()} fishing events detected in the last {condition.TimeWindowHours} hours",
            MarineProtectedAreaId = g.Key,
            Location = g.First().Location,
            Data = JsonSerializer.Serialize(new { EventCount = g.Count(), Vessels = g.Select(e => e.Vessel?.Name).Distinct() }),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        }).ToList();
    }

    private async Task<IReadOnlyList<Alert>> EvaluateVesselInMpaRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        var condition = JsonSerializer.Deserialize<VesselInMpaCondition>(rule.Conditions, JsonOptions);
        if (condition == null) return Array.Empty<Alert>();

        // Find vessels that have been inside MPAs
        var cutoffTime = DateTime.UtcNow.AddMinutes(-condition.MinDurationMinutes);

        var query = _context.VesselEvents
            .Include(e => e.Vessel)
            .Include(e => e.MarineProtectedArea)
            .Where(e => e.MarineProtectedAreaId != null)
            .Where(e => e.StartTime <= cutoffTime)
            .Where(e => e.EndTime == null || e.EndTime > DateTime.UtcNow.AddMinutes(-5)); // Still active

        if (rule.MarineProtectedAreaId.HasValue)
        {
            query = query.Where(e => e.MarineProtectedAreaId == rule.MarineProtectedAreaId);
        }

        if (condition.OnlyNoTakeZones)
        {
            query = query.Where(e => e.MarineProtectedArea!.ProtectionLevel == Domain.Enums.ProtectionLevel.NoTake);
        }

        var events = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return events.Select(e => new Alert
        {
            AlertRuleId = rule.Id,
            Type = AlertType.VesselInMPA,
            Severity = rule.Severity,
            Title = $"Vessel in Protected Area: {e.Vessel?.Name ?? "Unknown"}",
            Message = $"Vessel {e.Vessel?.Name} has been in {e.MarineProtectedArea?.Name} for over {condition.MinDurationMinutes} minutes",
            Location = e.Location,
            MarineProtectedAreaId = e.MarineProtectedAreaId,
            VesselId = e.VesselId,
            Data = JsonSerializer.Serialize(new { VesselName = e.Vessel?.Name, MpaName = e.MarineProtectedArea?.Name, e.StartTime }),
            ExpiresAt = DateTime.UtcNow.AddHours(6)
        }).ToList();
    }

    private async Task<IReadOnlyList<Alert>> EvaluateDhwRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        var condition = JsonSerializer.Deserialize<BleachingCondition>(rule.Conditions, JsonOptions);
        if (condition == null || !condition.MinDegreeHeatingWeek.HasValue) return Array.Empty<Alert>();

        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var query = _context.BleachingAlerts
            .Include(b => b.MarineProtectedArea)
            .Where(b => b.Date >= cutoffDate)
            .Where(b => b.DegreeHeatingWeek >= condition.MinDegreeHeatingWeek.Value);

        if (rule.MarineProtectedAreaId.HasValue)
        {
            query = query.Where(b => b.MarineProtectedAreaId == rule.MarineProtectedAreaId);
        }

        var alerts = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return alerts.Select(b => new Alert
        {
            AlertRuleId = rule.Id,
            Type = AlertType.DegreeHeatingWeek,
            Severity = rule.Severity,
            Title = $"High DHW: {b.DegreeHeatingWeek:F1}°C-weeks at {b.MarineProtectedArea?.Name ?? "Unknown"}",
            Message = $"Degree Heating Weeks has reached {b.DegreeHeatingWeek:F1}°C-weeks, indicating significant thermal stress",
            Location = b.Location,
            MarineProtectedAreaId = b.MarineProtectedAreaId,
            Data = JsonSerializer.Serialize(new { b.DegreeHeatingWeek, b.AlertLevel, b.SeaSurfaceTemperature }),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }).ToList();
    }

    private async Task<IReadOnlyList<Alert>> EvaluateCitizenObservationRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        var condition = JsonSerializer.Deserialize<CitizenObservationCondition>(rule.Conditions, JsonOptions);
        if (condition == null) return Array.Empty<Alert>();

        var cutoffTime = DateTime.UtcNow.AddHours(-24);
        var query = _context.CitizenObservations
            .Where(o => o.CreatedAt >= cutoffTime);

        // Filter by severity (lower = worse condition)
        if (condition.MaxHealthStatus.HasValue)
        {
            query = query.Where(o => o.Severity <= condition.MaxHealthStatus.Value);
        }

        if (!string.IsNullOrEmpty(condition.Keywords))
        {
            var keywords = condition.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(o => keywords.Any(k => o.Description != null && o.Description.Contains(k)));
        }

        var observations = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return observations.Select(o => new Alert
        {
            AlertRuleId = rule.Id,
            Type = AlertType.CitizenObservation,
            Severity = rule.Severity,
            Title = $"Citizen Observation: {o.Type} - Severity {o.Severity}",
            Message = o.Description ?? o.Title,
            Location = o.Location,
            Data = JsonSerializer.Serialize(new { o.Type, o.Severity, o.Title }),
            ExpiresAt = DateTime.UtcNow.AddDays(3)
        }).ToList();
    }
}
