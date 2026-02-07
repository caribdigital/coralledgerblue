using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.Commands.VerifyObservation;

using static GamificationConstants;

public record VerifyObservationCommand(
    Guid ObservationId,
    bool Approve,
    string? Notes = null
) : IRequest<VerifyObservationResult>;

public record VerifyObservationResult(
    bool Success,
    int PointsAwarded = 0,
    bool ProfileUpdated = false,
    ObserverTier? NewTier = null,
    string? Error = null);

public class VerifyObservationCommandHandler : IRequestHandler<VerifyObservationCommand, VerifyObservationResult>
{
    private readonly IMarineDbContext _context;
    private readonly IMediator _mediator;
    private readonly ILogger<VerifyObservationCommandHandler> _logger;

    public VerifyObservationCommandHandler(
        IMarineDbContext context,
        IMediator mediator,
        ILogger<VerifyObservationCommandHandler> logger)
    {
        _context = context;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<VerifyObservationResult> Handle(
        VerifyObservationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get observation
            var observation = await _context.CitizenObservations
                .FirstOrDefaultAsync(o => o.Id == request.ObservationId, cancellationToken)
                .ConfigureAwait(false);

            if (observation == null)
            {
                return new VerifyObservationResult(false, Error: "Observation not found");
            }

            if (string.IsNullOrWhiteSpace(observation.CitizenEmail))
            {
                return new VerifyObservationResult(false, Error: "Observation has no associated email");
            }

            // Update observation status
            if (request.Approve)
            {
                observation.Approve(request.Notes);
            }
            else
            {
                observation.Reject(request.Notes ?? "Rejected during verification");
            }

            // Get or create user profile
            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.CitizenEmail == observation.CitizenEmail, cancellationToken)
                .ConfigureAwait(false);

            if (profile == null)
            {
                profile = UserProfile.Create(observation.CitizenEmail, observation.CitizenName);
                _context.UserProfiles.Add(profile);
            }

            var oldTier = profile.Tier;
            int pointsAwarded = 0;

            // Update profile stats
            if (request.Approve)
            {
                profile.RecordVerifiedObservation();

                // Calculate points based on observation type and severity
                pointsAwarded = CalculatePoints(observation);

                // Award points if not already processed
                if (!observation.PointsProcessed)
                {
                    observation.AwardPoints(pointsAwarded);

                    // Award points via command
                    await _mediator.Send(new AwardPoints.AwardPointsCommand(
                        observation.CitizenEmail,
                        pointsAwarded,
                        $"Verified observation: {observation.Title}"), cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                profile.RecordRejectedObservation();
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Publish events for badge checking and point adjustments
            if (request.Approve)
            {
                await _mediator.Publish(
                    new ObservationVerifiedEvent(
                        observation.Id,
                        observation.CitizenEmail,
                        pointsAwarded),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _mediator.Publish(
                    new ObservationRejectedEvent(
                        observation.Id,
                        observation.CitizenEmail,
                        request.Notes ?? "Rejected during verification"),
                    cancellationToken).ConfigureAwait(false);
            }

            var newTier = profile.Tier != oldTier ? profile.Tier : (ObserverTier?)null;

            _logger.LogInformation("Verified observation {Id} for user {Email} - Approved: {Approved}, Points: {Points}",
                observation.Id, observation.CitizenEmail, request.Approve, pointsAwarded);

            return new VerifyObservationResult(
                Success: true,
                PointsAwarded: pointsAwarded,
                ProfileUpdated: true,
                NewTier: newTier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify observation {Id}", request.ObservationId);
            return new VerifyObservationResult(false, Error: ex.Message);
        }
    }

    private static int CalculatePoints(CitizenObservation observation)
    {
        // Base points
        int points = VerificationBasePoints;

        // Bonus for observation type
        points += observation.Type switch
        {
            ObservationType.CoralBleaching => CoralBleachingBonus,
            ObservationType.IllegalFishing => IllegalFishingBonus,
            ObservationType.WildlifeSighting => WildlifeSightingBonus,
            ObservationType.ReefHealth => ReefHealthBonus,
            _ => DefaultTypeBonus
        };

        // Bonus for severity
        points += observation.Severity * 2;

        // Bonus for photo evidence
        if (observation.Photos.Any())
        {
            points += PhotoEvidenceBonus;
        }

        return points;
    }
}
