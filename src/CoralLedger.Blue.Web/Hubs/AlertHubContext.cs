using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CoralLedger.Blue.Web.Hubs;

/// <summary>
/// Adapter that implements IAlertHubContext using SignalR
/// </summary>
public class AlertHubContext : IAlertHubContext
{
    private readonly IHubContext<AlertHub> _hubContext;

    public AlertHubContext(IHubContext<AlertHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToAllAsync(object alertData, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group("all-alerts").SendAsync("ReceiveAlert", alertData, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendToMpaAsync(Guid mpaId, object alertData, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"mpa-{mpaId}").SendAsync("ReceiveAlert", alertData, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendVesselPositionAsync(object positionData, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group("vessel-tracking").SendAsync("ReceiveVesselPosition", positionData, cancellationToken).ConfigureAwait(false);
    }
}
