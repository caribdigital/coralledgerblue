using System.Collections.Concurrent;
using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace CoralLedger.Blue.Infrastructure.Services;

// Alias to avoid conflict with WebPush.PushSubscription
using AppPushSubscription = CoralLedger.Blue.Application.Common.Interfaces.PushSubscription;

/// <summary>
/// Web push notification service using VAPID
/// </summary>
public class WebPushNotificationService : IPushNotificationService
{
    private readonly WebPushOptions _options;
    private readonly ILogger<WebPushNotificationService> _logger;
    private readonly WebPushClient? _client;
    private readonly VapidDetails? _vapidDetails;

    // In-memory subscription store (in production, use database)
    private static readonly ConcurrentDictionary<string, AppPushSubscription> _subscriptions = new();

    public WebPushNotificationService(
        IOptions<WebPushOptions> options,
        ILogger<WebPushNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.VapidPublicKey) &&
            !string.IsNullOrEmpty(_options.VapidPrivateKey))
        {
            _vapidDetails = new VapidDetails(
                _options.VapidSubject,
                _options.VapidPublicKey,
                _options.VapidPrivateKey);
            _client = new WebPushClient();
        }
    }

    public string GetVapidPublicKey() => _options.VapidPublicKey;

    public async Task<int> SendToAllAsync(
        string title,
        string message,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        if (_client is null || _vapidDetails is null)
        {
            _logger.LogWarning("VAPID keys not configured. Push notification not sent");
            return 0;
        }

        var successCount = 0;

        foreach (var subscription in _subscriptions.Values.ToList())
        {
            var sent = await SendToSubscriptionAsync(subscription, title, message, url, cancellationToken);
            if (sent)
            {
                successCount++;
            }
        }

        _logger.LogInformation("Sent push notification to {Count} subscribers: {Title}", successCount, title);
        return successCount;
    }

    public async Task<bool> SendToSubscriptionAsync(
        AppPushSubscription subscription,
        string title,
        string message,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
        if (_client is null || _vapidDetails is null)
        {
            _logger.LogWarning("VAPID keys not configured. Push notification not sent");
            return false;
        }

        try
        {
            var webPushSubscription = new WebPush.PushSubscription(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth);

            var payload = JsonSerializer.Serialize(new
            {
                title,
                body = message,
                icon = "/images/icons/icon-192x192.png",
                badge = "/images/icons/badge-72x72.png",
                url = url ?? "/dashboard",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            await _client.SendNotificationAsync(webPushSubscription, payload, _vapidDetails);

            _logger.LogDebug("Push notification sent to {Endpoint}", subscription.Endpoint);
            return true;
        }
        catch (WebPushException ex)
        {
            _logger.LogError(ex, "Failed to send push notification to {Endpoint}: {StatusCode}",
                subscription.Endpoint, ex.StatusCode);
            
            // Remove subscriptions that are gone or not found (no longer valid)
            if (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _subscriptions.TryRemove(subscription.Endpoint, out _);
                _logger.LogInformation("Removed invalid push subscription: {Endpoint}", subscription.Endpoint);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notification to {Endpoint}", subscription.Endpoint);
            return false;
        }
    }

    public Task RegisterSubscriptionAsync(
        AppPushSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        _subscriptions.AddOrUpdate(
            subscription.Endpoint,
            subscription,
            (_, _) => subscription);

        _logger.LogInformation("Registered push subscription: {Endpoint}", subscription.Endpoint);
        return Task.CompletedTask;
    }

    public Task UnregisterSubscriptionAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryRemove(endpoint, out _))
        {
            _logger.LogInformation("Unregistered push subscription: {Endpoint}", endpoint);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for Web Push notifications
/// </summary>
public class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>
    /// VAPID public key (base64 encoded)
    /// </summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>
    /// VAPID private key (base64 encoded)
    /// </summary>
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// VAPID subject (mailto: or https: URL)
    /// </summary>
    public string VapidSubject { get; set; } = "mailto:alerts@coralledgerblue.com";
}
