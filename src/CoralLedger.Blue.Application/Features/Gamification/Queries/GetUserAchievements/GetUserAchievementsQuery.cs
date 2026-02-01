using CoralLedger.Blue.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.Queries.GetUserAchievements;

public record GetUserAchievementsQuery(
    string CitizenEmail,
    bool CompletedOnly = false
) : IRequest<List<AchievementDto>>;

public record AchievementDto(
    string AchievementKey,
    string Title,
    string? Description,
    int CurrentProgress,
    int TargetProgress,
    int ProgressPercentage,
    bool IsCompleted,
    DateTime? CompletedAt,
    int PointsAwarded);

public class GetUserAchievementsQueryHandler : IRequestHandler<GetUserAchievementsQuery, List<AchievementDto>>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<GetUserAchievementsQueryHandler> _logger;

    public GetUserAchievementsQueryHandler(
        IMarineDbContext context,
        ILogger<GetUserAchievementsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<AchievementDto>> Handle(
        GetUserAchievementsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.UserAchievements
                .Where(a => a.CitizenEmail == request.CitizenEmail);

            if (request.CompletedOnly)
            {
                query = query.Where(a => a.IsCompleted);
            }

            var achievements = await query
                .OrderByDescending(a => a.IsCompleted)
                .ThenByDescending(a => a.CurrentProgress)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return achievements
                .Select(a => new AchievementDto(
                    AchievementKey: a.AchievementKey,
                    Title: a.Title,
                    Description: a.Description,
                    CurrentProgress: a.CurrentProgress,
                    TargetProgress: a.TargetProgress,
                    ProgressPercentage: a.GetProgressPercentage(),
                    IsCompleted: a.IsCompleted,
                    CompletedAt: a.CompletedAt,
                    PointsAwarded: a.PointsAwarded))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get achievements for user {Email}", request.CitizenEmail);
            return new List<AchievementDto>();
        }
    }
}
