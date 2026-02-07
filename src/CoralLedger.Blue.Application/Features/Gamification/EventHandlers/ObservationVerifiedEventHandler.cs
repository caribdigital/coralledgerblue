using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardBadge;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.EventHandlers;

using static GamificationConstants;

/// <summary>
/// Handles the ObservationVerifiedEvent to award badges when criteria are met
/// </summary>
public class ObservationVerifiedEventHandler : INotificationHandler<ObservationVerifiedEvent>
{
    private readonly IMediator _mediator;
    private readonly IMarineDbContext _context;
    private readonly ILogger<ObservationVerifiedEventHandler> _logger;

    public ObservationVerifiedEventHandler(
        IMediator mediator,
        IMarineDbContext context,
        ILogger<ObservationVerifiedEventHandler> logger)
    {
        _mediator = mediator;
        _context = context;
        _logger = logger;
    }

    public async Task Handle(ObservationVerifiedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await CheckAndAwardBadgesAsync(notification.CitizenEmail, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking badges for user {Email} after observation {ObservationId} verification",
                notification.CitizenEmail, notification.ObservationId);
        }
    }

    private async Task CheckAndAwardBadgesAsync(string citizenEmail, CancellationToken cancellationToken)
    {
        // Get observation statistics for the user
        var observations = await _context.CitizenObservations
            .Where(o => o.CitizenEmail == citizenEmail)
            .ToListAsync(cancellationToken);

        var totalObservations = observations.Count;
        var verifiedObservations = observations.Count(o => o.Status == ObservationStatus.Approved);
        var rejectedObservations = observations.Count(o => o.Status == ObservationStatus.Rejected);

        // Calculate accuracy rate
        var totalProcessed = verifiedObservations + rejectedObservations;
        var accuracyRate = totalProcessed > 0 ? (double)verifiedObservations / totalProcessed * 100 : 0;

        // Check for FirstVerifiedObservation badge
        if (verifiedObservations == 1)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.FirstVerifiedObservation,
                "Your first observation has been verified!", cancellationToken);
        }

        // Check for quantity milestone badges
        if (totalObservations >= TenObservationsThreshold && totalObservations < FiftyObservationsThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.TenObservations,
                $"Submitted {TenObservationsThreshold} observations", cancellationToken);
        }
        else if (totalObservations >= FiftyObservationsThreshold && totalObservations < HundredObservationsThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.FiftyObservations,
                $"Submitted {FiftyObservationsThreshold} observations", cancellationToken);
        }
        else if (totalObservations >= HundredObservationsThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.HundredObservations,
                $"Submitted {HundredObservationsThreshold} observations", cancellationToken);
        }

        // Check for AccurateObserver badge (90%+ accuracy with 20+ verified)
        if (verifiedObservations >= AccurateObserverMinVerified && accuracyRate >= AccurateObserverMinAccuracy)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.AccurateObserver,
                $"Achieved {accuracyRate:F1}% accuracy rate with {verifiedObservations} verified observations",
                cancellationToken);
        }

        // Check for CoralExpert badge (25+ verified coral-related observations)
        var coralTypes = new[] { ObservationType.CoralBleaching, ObservationType.ReefHealth };
        var verifiedCoralCount = observations.Count(o =>
            coralTypes.Contains(o.Type) && o.Status == ObservationStatus.Approved);
        if (verifiedCoralCount >= CoralExpertThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.CoralExpert,
                $"Submitted {CoralExpertThreshold}+ verified coral observations", cancellationToken);
        }

        // Check for BleachingDetector badge (awarded for first verified bleaching report)
        var firstBleachingObservation = observations
            .FirstOrDefault(o => o.Type == ObservationType.CoralBleaching && o.Status == ObservationStatus.Approved);
        if (firstBleachingObservation != null)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.BleachingDetector,
                "First verified bleaching report", cancellationToken);
        }

        // Check for MPAGuardian badge (10+ observations within MPA)
        var mpaObservations = observations.Count(o => o.IsInMpa == true);
        if (mpaObservations >= MpaGuardianThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.MPAGuardian,
                $"Submitted {MpaGuardianThreshold}+ observations within MPA boundaries", cancellationToken);
        }

        // Check for time-based contributor badges
        await CheckContributorBadgesAsync(citizenEmail, observations, cancellationToken);
    }

    private async Task CheckContributorBadgesAsync(
        string citizenEmail,
        List<Domain.Entities.CitizenObservation> observations,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Weekly contributor: 7+ observations in current week
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        var weeklyCount = observations.Count(o => o.CreatedAt >= weekStart);
        if (weeklyCount >= WeeklyContributorThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.WeeklyContributor,
                $"Submitted {WeeklyContributorThreshold}+ observations this week", cancellationToken);
        }

        // Monthly contributor: 30+ observations in current month
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthlyCount = observations.Count(o => o.CreatedAt >= monthStart);
        if (monthlyCount >= MonthlyContributorThreshold)
        {
            await AwardBadgeIfNewAsync(citizenEmail, BadgeType.MonthlyContributor,
                $"Submitted {MonthlyContributorThreshold}+ observations this month", cancellationToken);
        }
    }

    private async Task AwardBadgeIfNewAsync(
        string citizenEmail,
        BadgeType badgeType,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new AwardBadgeCommand(citizenEmail, badgeType, description),
                cancellationToken);

            if (result.Success && !result.AlreadyEarned)
            {
                _logger.LogInformation(
                    "Awarded badge {BadgeType} to user {Email}",
                    badgeType, citizenEmail);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to award badge {BadgeType} to user {Email}",
                badgeType, citizenEmail);
        }
    }
}
