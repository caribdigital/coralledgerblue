using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.EventHandlers;

/// <summary>
/// Handles the ObservationRejectedEvent to deduct points when an observation is rejected
/// </summary>
public class ObservationRejectedEventHandler : INotificationHandler<ObservationRejectedEvent>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<ObservationRejectedEventHandler> _logger;

    public ObservationRejectedEventHandler(
        IMarineDbContext context,
        ILogger<ObservationRejectedEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(ObservationRejectedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Deduct points for rejected observation (anti-gaming measure)
            var userPoints = await _context.UserPoints
                .FirstOrDefaultAsync(p => p.CitizenEmail == notification.CitizenEmail, cancellationToken);

            if (userPoints != null)
            {
                userPoints.DeductPoints(5, $"Observation rejected: {notification.Reason}");
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Deducted 5 points from {Email} for rejected observation {ObservationId}",
                    notification.CitizenEmail, notification.ObservationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deducting points for rejected observation {ObservationId}",
                notification.ObservationId);
        }
    }
}
