using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.Queries.GetLeaderboard;

public record GetLeaderboardQuery(
    LeaderboardPeriod Period = LeaderboardPeriod.AllTime,
    int PageSize = 50,
    int PageNumber = 1
) : IRequest<LeaderboardDto>;

public enum LeaderboardPeriod
{
    Weekly,
    Monthly,
    AllTime
}

public record LeaderboardDto(
    LeaderboardPeriod Period,
    List<LeaderboardEntryDto> Entries,
    int TotalCount);

public record LeaderboardEntryDto(
    int Rank,
    string CitizenEmail,
    string? CitizenName,
    int Points,
    ObserverTier Tier,
    int VerifiedObservations);

public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, LeaderboardDto>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<GetLeaderboardQueryHandler> _logger;

    public GetLeaderboardQueryHandler(
        IMarineDbContext context,
        ILogger<GetLeaderboardQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LeaderboardDto> Handle(
        GetLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query user points based on period
            var pointsQuery = _context.UserPoints.AsQueryable();

            var orderedQuery = request.Period switch
            {
                LeaderboardPeriod.Weekly => pointsQuery.OrderByDescending(p => p.WeeklyPoints),
                LeaderboardPeriod.Monthly => pointsQuery.OrderByDescending(p => p.MonthlyPoints),
                _ => pointsQuery.OrderByDescending(p => p.TotalPoints)
            };

            // Get total count
            var totalCount = await orderedQuery.CountAsync(cancellationToken).ConfigureAwait(false);

            // Get paginated results
            var userPoints = await orderedQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Get profiles for these users
            var emails = userPoints.Select(p => p.CitizenEmail).ToList();
            var profiles = await _context.UserProfiles
                .Where(p => emails.Contains(p.CitizenEmail))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var profileDict = profiles.ToDictionary(p => p.CitizenEmail);

            // Build leaderboard entries
            var entries = new List<LeaderboardEntryDto>();
            int rank = (request.PageNumber - 1) * request.PageSize + 1;

            foreach (var userPoint in userPoints)
            {
                var profile = profileDict.GetValueOrDefault(userPoint.CitizenEmail);

                var points = request.Period switch
                {
                    LeaderboardPeriod.Weekly => userPoint.WeeklyPoints,
                    LeaderboardPeriod.Monthly => userPoint.MonthlyPoints,
                    _ => userPoint.TotalPoints
                };

                entries.Add(new LeaderboardEntryDto(
                    Rank: rank++,
                    CitizenEmail: userPoint.CitizenEmail,
                    CitizenName: profile?.CitizenName,
                    Points: points,
                    Tier: profile?.Tier ?? ObserverTier.None,
                    VerifiedObservations: profile?.VerifiedObservations ?? 0));
            }

            return new LeaderboardDto(
                Period: request.Period,
                Entries: entries,
                TotalCount: totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get leaderboard for period {Period}", request.Period);
            return new LeaderboardDto(request.Period, new List<LeaderboardEntryDto>(), 0);
        }
    }
}
