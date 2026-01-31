using Microsoft.AspNetCore.SignalR;

namespace CoralLedger.Blue.Web.Hubs;

/// <summary>
/// SignalR hub for real-time alert notifications
/// </summary>
public class AlertHub : Hub
{
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(ILogger<AlertHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribe to alerts for specific MPAs
    /// </summary>
    public async Task SubscribeToMpa(string mpaId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mpa-{mpaId}").ConfigureAwait(false);
        _logger.LogInformation("Client {ConnectionId} subscribed to MPA {MpaId}", Context.ConnectionId, mpaId);
    }

    /// <summary>
    /// Unsubscribe from MPA alerts
    /// </summary>
    public async Task UnsubscribeFromMpa(string mpaId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"mpa-{mpaId}").ConfigureAwait(false);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from MPA {MpaId}", Context.ConnectionId, mpaId);
    }

    /// <summary>
    /// Subscribe to all alerts
    /// </summary>
    public async Task SubscribeToAllAlerts()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-alerts").ConfigureAwait(false);
        _logger.LogInformation("Client {ConnectionId} subscribed to all alerts", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to vessel position updates
    /// </summary>
    public async Task SubscribeToVesselTracking()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "vessel-tracking").ConfigureAwait(false);
        _logger.LogInformation("Client {ConnectionId} subscribed to vessel tracking", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from vessel tracking
    /// </summary>
    public async Task UnsubscribeFromVesselTracking()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "vessel-tracking").ConfigureAwait(false);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from vessel tracking", Context.ConnectionId);
    }
}
