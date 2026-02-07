using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardBadge;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardPoints;
using CoralLedger.Blue.Application.Features.Gamification.Queries.GetLeaderboard;
using CoralLedger.Blue.Application.Features.Gamification.Queries.GetUserAchievements;
using CoralLedger.Blue.Application.Features.Gamification.Queries.GetUserProfile;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Web.Services;
using MediatR;

namespace CoralLedger.Blue.Web.Endpoints;

public static class GamificationEndpoints
{
    private const int DefaultPageSize = 50;
    private const int DefaultPageNumber = 1;
    private const int MaxObserverLeaderboardSize = 100;
    private const int TopObserversLimit = 10;

    public static IEndpointRouteBuilder MapGamificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gamification")
            .WithTags("Gamification");

        // GET /api/gamification/profile/{email} - Get user's gamification profile
        group.MapGet("/profile/{email}", async (
            string email,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var profile = await mediator.Send(new GetUserProfileQuery(email), ct).ConfigureAwait(false);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        })
        .WithName("GetUserProfile")
        .WithDescription("Get user's gamification profile including tier, stats, points, and badges")
        .Produces<UserProfileDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/gamification/leaderboard - Get leaderboard
        group.MapGet("/leaderboard", async (
            IMediator mediator,
            CancellationToken ct = default,
            string? period = null,
            int pageSize = DefaultPageSize,
            int pageNumber = DefaultPageNumber) =>
        {
            // Parse period parameter
            LeaderboardPeriod periodEnum = LeaderboardPeriod.AllTime;
            if (!string.IsNullOrEmpty(period) && Enum.TryParse<LeaderboardPeriod>(period, true, out var parsed))
            {
                periodEnum = parsed;
            }

            var query = new GetLeaderboardQuery(periodEnum, pageSize, pageNumber);
            var leaderboard = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(leaderboard);
        })
        .WithName("GetLeaderboard")
        .WithDescription("Get leaderboard rankings by period (Weekly, Monthly, AllTime)")
        .Produces<LeaderboardDto>();

        // GET /api/gamification/leaderboard/observers - Get top observers by tier
        group.MapGet("/leaderboard/observers", async (
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            // Get all-time leaderboard filtered to Gold/Silver/Bronze observers
            var query = new GetLeaderboardQuery(LeaderboardPeriod.AllTime, MaxObserverLeaderboardSize, DefaultPageNumber);
            var leaderboard = await mediator.Send(query, ct).ConfigureAwait(false);
            
            // Filter to users with tiers and take top 10
            var topObservers = leaderboard.Entries
                .Where(e => e.Tier != ObserverTier.None)
                .OrderByDescending(e => e.Tier)
                .ThenByDescending(e => e.VerifiedObservations)
                .Take(TopObserversLimit)
                .ToList();

            return Results.Ok(new { entries = topObservers, count = topObservers.Count });
        })
        .WithName("GetTopObservers")
        .WithDescription("Get top observers ranked by tier and verified observations")
        .Produces<object>();

        // GET /api/gamification/badges - Get all badge definitions
        group.MapGet("/badges", () =>
        {
            var badges = Enum.GetValues<BadgeType>()
                .Select(b => new BadgeDefinitionDto(
                    b,
                    BadgeDisplayHelper.GetBadgeName(b),
                    BadgeDisplayHelper.GetBadgeDescription(b),
                    BadgeDisplayHelper.GetBadgeRequirement(b)))
                .ToList();

            return Results.Ok(badges);
        })
        .WithName("GetBadgeDefinitions")
        .WithDescription("Get all available badge types with descriptions and requirements")
        .Produces<List<BadgeDefinitionDto>>();

        // GET /api/gamification/achievements/{email} - Get user's achievements
        group.MapGet("/achievements/{email}", async (
            string email,
            bool? completedOnly,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var query = new GetUserAchievementsQuery(email, completedOnly ?? false);
            var achievements = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(achievements);
        })
        .WithName("GetUserAchievements")
        .WithDescription("Get user's achievements with progress tracking")
        .Produces<List<AchievementDto>>();

        // Admin endpoints group - requires authentication
        var adminGroup = endpoints.MapGroup("/api/admin/gamification")
            .WithTags("Gamification - Admin")
            .RequireAuthorization();

        // POST /api/admin/gamification/points - Award points to a user
        adminGroup.MapPost("/points", async (
            AwardPointsRequest request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            // Validate points are positive
            if (request.Points <= 0)
            {
                return Results.BadRequest(new { error = "Points must be a positive number" });
            }

            var command = new AwardPointsCommand(request.Email, request.Points, request.Reason);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(result);
        })
        .WithName("AwardPoints")
        .WithDescription("Manually award points to a user (admin only)")
        .Produces<AwardPointsResult>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/admin/gamification/badges - Award badge to a user
        adminGroup.MapPost("/badges", async (
            AwardBadgeRequest request,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            // Parse badge type
            if (!Enum.TryParse<BadgeType>(request.BadgeType, true, out var badgeType))
            {
                return Results.BadRequest(new { error = "Invalid badge type" });
            }

            var command = new AwardBadgeCommand(request.Email, badgeType, request.Description);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            
            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(result);
        })
        .WithName("AwardBadge")
        .WithDescription("Manually award a badge to a user (admin only)")
        .Produces<AwardBadgeResult>()
        .Produces(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}

/// <summary>
/// Request to award points to a user
/// </summary>
public record AwardPointsRequest(
    string Email,
    int Points,
    string Reason);

/// <summary>
/// Request to award a badge to a user
/// </summary>
public record AwardBadgeRequest(
    string Email,
    string BadgeType,
    string? Description = null);

/// <summary>
/// Badge definition DTO
/// </summary>
public record BadgeDefinitionDto(
    BadgeType BadgeType,
    string Name,
    string Description,
    string Requirement);
