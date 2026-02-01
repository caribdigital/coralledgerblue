using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.Queries.GetUserProfile;

public record GetUserProfileQuery(
    string CitizenEmail
) : IRequest<UserProfileDto?>;

public record UserProfileDto(
    string CitizenEmail,
    string? CitizenName,
    ObserverTier Tier,
    int TotalObservations,
    int VerifiedObservations,
    int RejectedObservations,
    double AccuracyRate,
    int TotalPoints,
    int WeeklyPoints,
    int MonthlyPoints,
    List<BadgeDto> Badges,
    DateTime? LastObservationAt);

public record BadgeDto(
    BadgeType BadgeType,
    string? Description,
    DateTime EarnedAt);

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserProfileDto?>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<GetUserProfileQueryHandler> _logger;

    public GetUserProfileQueryHandler(
        IMarineDbContext context,
        ILogger<GetUserProfileQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserProfileDto?> Handle(
        GetUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get profile
            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.CitizenEmail == request.CitizenEmail, cancellationToken)
                .ConfigureAwait(false);

            if (profile == null)
            {
                return null;
            }

            // Get points
            var points = await _context.UserPoints
                .FirstOrDefaultAsync(p => p.CitizenEmail == request.CitizenEmail, cancellationToken)
                .ConfigureAwait(false);

            // Get badges
            var badges = await _context.UserBadges
                .Where(b => b.CitizenEmail == request.CitizenEmail)
                .OrderByDescending(b => b.EarnedAt)
                .Select(b => new BadgeDto(b.BadgeType, b.Description, b.EarnedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new UserProfileDto(
                CitizenEmail: profile.CitizenEmail,
                CitizenName: profile.CitizenName,
                Tier: profile.Tier,
                TotalObservations: profile.TotalObservations,
                VerifiedObservations: profile.VerifiedObservations,
                RejectedObservations: profile.RejectedObservations,
                AccuracyRate: profile.AccuracyRate,
                TotalPoints: points?.TotalPoints ?? 0,
                WeeklyPoints: points?.WeeklyPoints ?? 0,
                MonthlyPoints: points?.MonthlyPoints ?? 0,
                Badges: badges,
                LastObservationAt: profile.LastObservationAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user profile for {Email}", request.CitizenEmail);
            return null;
        }
    }
}
