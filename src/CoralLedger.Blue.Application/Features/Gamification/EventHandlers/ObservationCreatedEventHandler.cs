using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardPoints;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.EventHandlers;

/// <summary>
/// Handles the ObservationCreatedEvent to award points when a citizen observation is submitted
/// </summary>
public class ObservationCreatedEventHandler : INotificationHandler<ObservationCreatedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ObservationCreatedEventHandler> _logger;

    public ObservationCreatedEventHandler(
        IMediator mediator,
        ILogger<ObservationCreatedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(ObservationCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Skip if no email provided (anonymous observations)
        if (string.IsNullOrWhiteSpace(notification.CitizenEmail))
        {
            _logger.LogDebug("Skipping points award for observation {ObservationId} - no email provided",
                notification.ObservationId);
            return;
        }

        try
        {
            int points = 0;
            var reasons = new List<string>();

            // Base points for submitting an observation
            points += GamificationConstants.BaseObservationPoints;
            reasons.Add("Observation submitted");

            // Bonus for including photos
            if (notification.HasPhotos)
            {
                points += GamificationConstants.PhotoBonusPoints;
                reasons.Add("Photo included");
            }

            // Bonus for GPS coordinates (always true if observation is created with location)
            if (notification.HasLocation)
            {
                points += GamificationConstants.GpsBonusPoints;
                reasons.Add("GPS coordinates provided");
            }

            // Bonus for observation within MPA
            if (notification.IsInMpa)
            {
                points += GamificationConstants.MpaBonusPoints;
                reasons.Add("Within MPA");
            }

            // Award the points
            var result = await _mediator.Send(
                new AwardPointsCommand(
                    notification.CitizenEmail,
                    points,
                    string.Join(", ", reasons)),
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Awarded {Points} points to {Email} for observation {ObservationId}",
                    points, notification.CitizenEmail, notification.ObservationId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to award points for observation {ObservationId}: {Error}",
                    notification.ObservationId, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error awarding points for observation {ObservationId}",
                notification.ObservationId);
        }
    }
}
