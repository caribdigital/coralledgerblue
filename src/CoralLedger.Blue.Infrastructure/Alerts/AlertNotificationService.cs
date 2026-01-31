using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Infrastructure.Alerts;

/// <summary>
/// Service for sending alert notifications through configured channels
/// </summary>
public class AlertNotificationService : IAlertNotificationService
{
    private readonly IAlertHubContext _hubContext;
    private readonly IEmailService _emailService;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<AlertNotificationService> _logger;

    public AlertNotificationService(
        IAlertHubContext hubContext,
        IEmailService emailService,
        IPushNotificationService pushService,
        ILogger<AlertNotificationService> logger)
    {
        _hubContext = hubContext;
        _emailService = emailService;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Alert alert, AlertRule rule, CancellationToken cancellationToken = default)
    {
        var channels = rule.NotificationChannels;
        var tasks = new List<Task>();

        // Real-time via SignalR
        if (channels.HasFlag(NotificationChannel.RealTime) || channels.HasFlag(NotificationChannel.Dashboard))
        {
            tasks.Add(SendRealTimeNotificationAsync(alert, cancellationToken));
        }

        // Email notification
        if (channels.HasFlag(NotificationChannel.Email) && !string.IsNullOrEmpty(rule.NotificationEmails))
        {
            tasks.Add(SendEmailNotificationAsync(alert, rule.NotificationEmails, cancellationToken));
        }

        // Push notification
        if (channels.HasFlag(NotificationChannel.Push))
        {
            tasks.Add(SendPushNotificationAsync(alert, cancellationToken));
        }

        // Wait for all notifications to be sent
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task SendRealTimeNotificationAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        var alertData = new
        {
            id = alert.Id,
            type = alert.Type.ToString(),
            severity = alert.Severity.ToString(),
            title = alert.Title,
            message = alert.Message,
            mpaId = alert.MarineProtectedAreaId,
            vesselId = alert.VesselId,
            createdAt = alert.CreatedAt,
            location = alert.Location != null ? new { lon = alert.Location.X, lat = alert.Location.Y } : null
        };

        // Send to all alert subscribers
        await _hubContext.SendToAllAsync(alertData, cancellationToken).ConfigureAwait(false);

        // Send to MPA-specific subscribers
        if (alert.MarineProtectedAreaId.HasValue)
        {
            await _hubContext.SendToMpaAsync(alert.MarineProtectedAreaId.Value, alertData, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Sent real-time alert: {Title}", alert.Title);
    }

    private async Task SendEmailNotificationAsync(Alert alert, string emails, CancellationToken cancellationToken)
    {
        try
        {
            var mpaName = alert.MarineProtectedArea?.Name;
            var success = await _emailService.SendAlertEmailAsync(
                emails,
                alert.Title,
                alert.Message,
                alert.Severity.ToString(),
                mpaName,
                cancellationToken).ConfigureAwait(false);

            if (success)
            {
                _logger.LogInformation("Email notification sent for alert {AlertId} to {Emails}",
                    alert.Id, emails);
            }
            else
            {
                _logger.LogWarning("Failed to send email notification for alert {AlertId}", alert.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification for alert {AlertId}", alert.Id);
        }
    }

    private async Task SendPushNotificationAsync(Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            var url = alert.MarineProtectedAreaId.HasValue
                ? $"/map?mpaId={alert.MarineProtectedAreaId}"
                : "/dashboard";

            var count = await _pushService.SendToAllAsync(
                alert.Title,
                alert.Message,
                url,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Push notification sent to {Count} subscribers for alert {AlertId}",
                count, alert.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification for alert {AlertId}", alert.Id);
        }
    }
}
