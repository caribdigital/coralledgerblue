using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/alerts")
            .WithTags("Alerts");

        // GET /api/alerts - Get recent alerts
        group.MapGet("/", async (
            IMarineDbContext context,
            AlertType? type = null,
            AlertSeverity? minSeverity = null,
            bool? acknowledged = null,
            int limit = 50,
            CancellationToken ct = default) =>
        {
            var query = context.Alerts
                .Include(a => a.AlertRule)
                .Include(a => a.MarineProtectedArea)
                .Include(a => a.Vessel)
                .AsQueryable();

            if (type.HasValue)
                query = query.Where(a => a.Type == type.Value);

            if (minSeverity.HasValue)
                query = query.Where(a => a.Severity >= minSeverity.Value);

            if (acknowledged.HasValue)
                query = query.Where(a => a.IsAcknowledged == acknowledged.Value);

            var alerts = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .Select(a => new
                {
                    a.Id,
                    Type = a.Type.ToString(),
                    Severity = a.Severity.ToString(),
                    a.Title,
                    a.Message,
                    MpaName = a.MarineProtectedArea != null ? a.MarineProtectedArea.Name : null,
                    VesselName = a.Vessel != null ? a.Vessel.Name : null,
                    a.IsAcknowledged,
                    a.CreatedAt,
                    Location = a.Location != null ? new { Lon = a.Location.X, Lat = a.Location.Y } : null
                })
                .ToListAsync(ct).ConfigureAwait(false);

            return Results.Ok(alerts);
        })
        .WithName("GetAlerts")
        .Produces<object>();

        // GET /api/alerts/{id} - Get specific alert
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var alert = await context.Alerts
                .Include(a => a.AlertRule)
                .Include(a => a.MarineProtectedArea)
                .Include(a => a.Vessel)
                .FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);

            if (alert == null)
                return Results.NotFound();

            return Results.Ok(new
            {
                alert.Id,
                Type = alert.Type.ToString(),
                Severity = alert.Severity.ToString(),
                alert.Title,
                alert.Message,
                alert.Data,
                MpaName = alert.MarineProtectedArea?.Name,
                VesselName = alert.Vessel?.Name,
                alert.IsAcknowledged,
                alert.AcknowledgedBy,
                alert.AcknowledgedAt,
                alert.CreatedAt,
                Location = alert.Location != null ? new { Lon = alert.Location.X, Lat = alert.Location.Y } : null,
                Rule = alert.AlertRule != null ? new { alert.AlertRule.Id, alert.AlertRule.Name } : null
            });
        })
        .WithName("GetAlert")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/alerts/{id}/acknowledge - Acknowledge an alert
        group.MapPost("/{id:guid}/acknowledge", async (
            Guid id,
            AcknowledgeRequest request,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var alert = await context.Alerts.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
            if (alert == null)
                return Results.NotFound();

            alert.Acknowledge(request.AcknowledgedBy ?? "System");

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new { alert.Id, alert.IsAcknowledged, alert.AcknowledgedAt });
        })
        .WithName("AcknowledgeAlert")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/alerts/rules - Get all alert rules
        group.MapGet("/rules", async (
            IMarineDbContext context,
            bool? activeOnly = null,
            CancellationToken ct = default) =>
        {
            var query = context.AlertRules
                .Include(r => r.MarineProtectedArea)
                .AsQueryable();

            if (activeOnly == true)
                query = query.Where(r => r.IsActive);

            var rules = await query
                .OrderBy(r => r.Name)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Description,
                    Type = r.Type.ToString(),
                    Severity = r.Severity.ToString(),
                    r.IsActive,
                    MpaName = r.MarineProtectedArea != null ? r.MarineProtectedArea.Name : null,
                    r.CreatedAt,
                    r.LastTriggeredAt
                })
                .ToListAsync(ct).ConfigureAwait(false);

            return Results.Ok(rules);
        })
        .WithName("GetAlertRules")
        .Produces<object>();

        // POST /api/alerts/rules - Create a new alert rule
        group.MapPost("/rules", async (
            CreateAlertRuleRequest request,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            if (!Enum.TryParse<AlertType>(request.Type, true, out var alertType))
                return Results.BadRequest("Invalid alert type");

            if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
                severity = AlertSeverity.Medium;

            var rule = AlertRule.Create(
                name: request.Name,
                type: alertType,
                conditions: request.Conditions,
                description: request.Description,
                severity: severity,
                marineProtectedAreaId: request.MarineProtectedAreaId,
                notificationChannels: request.NotificationChannels ?? NotificationChannel.Dashboard | NotificationChannel.RealTime,
                notificationEmails: request.NotificationEmails,
                cooldownPeriod: TimeSpan.FromMinutes(request.CooldownMinutes ?? 60)
            );

            // Deactivate if requested
            if (request.IsActive == false)
            {
                rule.Deactivate();
            }

            context.AlertRules.Add(rule);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Created($"/api/alerts/rules/{rule.Id}", new { rule.Id, rule.Name });
        })
        .WithName("CreateAlertRule")
        .Produces<object>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // PUT /api/alerts/rules/{id} - Update an alert rule
        group.MapPut("/rules/{id:guid}", async (
            Guid id,
            UpdateAlertRuleRequest request,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var rule = await context.AlertRules.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
            if (rule == null)
                return Results.NotFound();

            // Update basic properties
            if (request.Name != null) rule.Name = request.Name;
            if (request.Description != null) rule.Description = request.Description;

            // Update conditions using domain method
            if (request.Conditions != null) rule.UpdateConditions(request.Conditions);

            // Update severity using domain method
            if (request.Severity != null && Enum.TryParse<AlertSeverity>(request.Severity, true, out var sev))
                rule.UpdateSeverity(sev);

            // Update active state using domain methods
            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                    rule.Activate();
                else
                    rule.Deactivate();
            }

            // Update notification settings using domain method
            if (request.NotificationChannels.HasValue || request.NotificationEmails != null || request.CooldownMinutes.HasValue)
            {
                var channels = request.NotificationChannels ?? rule.NotificationChannels;
                var emails = request.NotificationEmails ?? rule.NotificationEmails;
                var cooldown = request.CooldownMinutes.HasValue
                    ? TimeSpan.FromMinutes(request.CooldownMinutes.Value)
                    : (TimeSpan?)null;
                rule.UpdateNotificationSettings(channels, emails, cooldown);
            }
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new { rule.Id, rule.Name, rule.IsActive });
        })
        .WithName("UpdateAlertRule")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/alerts/rules/{id} - Delete an alert rule
        group.MapDelete("/rules/{id:guid}", async (
            Guid id,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var rule = await context.AlertRules.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
            if (rule == null)
                return Results.NotFound();

            context.AlertRules.Remove(rule);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.NoContent();
        })
        .WithName("DeleteAlertRule")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/alerts/rules/{id}/evaluate - Manually trigger rule evaluation
        group.MapPost("/rules/{id:guid}/evaluate", async (
            Guid id,
            IAlertRuleEngine engine,
            CancellationToken ct = default) =>
        {
            var alerts = await engine.EvaluateRuleAsync(id, ct).ConfigureAwait(false);
            var count = await engine.PersistAlertsAsync(alerts, ct).ConfigureAwait(false);

            return Results.Ok(new { AlertsGenerated = count });
        })
        .WithName("EvaluateAlertRule")
        .Produces<object>();

        // POST /api/alerts/evaluate-all - Evaluate all active rules
        group.MapPost("/evaluate-all", async (
            IAlertRuleEngine engine,
            CancellationToken ct = default) =>
        {
            var alerts = await engine.EvaluateAllRulesAsync(ct).ConfigureAwait(false);
            var count = await engine.PersistAlertsAsync(alerts, ct).ConfigureAwait(false);

            return Results.Ok(new { AlertsGenerated = count });
        })
        .WithName("EvaluateAllAlertRules")
        .Produces<object>();

        // GET /api/alerts/stats - Get alert statistics
        group.MapGet("/stats", async (
            IMarineDbContext context,
            int days = 7,
            CancellationToken ct = default) =>
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);

            var stats = await context.Alerts
                .Where(a => a.CreatedAt >= cutoff)
                .GroupBy(a => a.Type)
                .Select(g => new
                {
                    Type = g.Key.ToString(),
                    Count = g.Count(),
                    Acknowledged = g.Count(a => a.IsAcknowledged),
                    Critical = g.Count(a => a.Severity == AlertSeverity.Critical),
                    High = g.Count(a => a.Severity == AlertSeverity.High)
                })
                .ToListAsync(ct).ConfigureAwait(false);

            var totalUnacknowledged = await context.Alerts
                .CountAsync(a => !a.IsAcknowledged, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                Period = $"Last {days} days",
                TotalUnacknowledged = totalUnacknowledged,
                ByType = stats
            });
        })
        .WithName("GetAlertStats")
        .Produces<object>();

        return endpoints;
    }
}

public record AcknowledgeRequest(string? AcknowledgedBy);

public record CreateAlertRuleRequest(
    string Name,
    string? Description,
    string Type,
    string? Severity,
    bool? IsActive,
    string Conditions,
    Guid? MarineProtectedAreaId,
    NotificationChannel? NotificationChannels,
    string? NotificationEmails,
    int? CooldownMinutes);

public record UpdateAlertRuleRequest(
    string? Name,
    string? Description,
    string? Severity,
    bool? IsActive,
    string? Conditions,
    NotificationChannel? NotificationChannels,
    string? NotificationEmails,
    int? CooldownMinutes);
