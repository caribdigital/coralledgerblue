using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.Commands.AwardBadge;

public record AwardBadgeCommand(
    string CitizenEmail,
    BadgeType BadgeType,
    string? Description = null
) : IRequest<AwardBadgeResult>;

public record AwardBadgeResult(
    bool Success,
    Guid? BadgeId = null,
    bool AlreadyEarned = false,
    string? Error = null);

public class AwardBadgeCommandHandler : IRequestHandler<AwardBadgeCommand, AwardBadgeResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<AwardBadgeCommandHandler> _logger;

    public AwardBadgeCommandHandler(
        IMarineDbContext context,
        ILogger<AwardBadgeCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AwardBadgeResult> Handle(
        AwardBadgeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if badge already earned
            var existingBadge = await _context.UserBadges
                .FirstOrDefaultAsync(
                    b => b.CitizenEmail == request.CitizenEmail && b.BadgeType == request.BadgeType,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existingBadge != null)
            {
                return new AwardBadgeResult(
                    Success: true,
                    BadgeId: existingBadge.Id,
                    AlreadyEarned: true);
            }

            // Create new badge
            var badge = UserBadge.Create(
                request.CitizenEmail,
                request.BadgeType,
                request.Description);

            _context.UserBadges.Add(badge);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Awarded badge {BadgeType} to user {Email}",
                request.BadgeType, request.CitizenEmail);

            return new AwardBadgeResult(
                Success: true,
                BadgeId: badge.Id,
                AlreadyEarned: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to award badge {BadgeType} to user {Email}",
                request.BadgeType, request.CitizenEmail);
            return new AwardBadgeResult(false, Error: ex.Message);
        }
    }
}
