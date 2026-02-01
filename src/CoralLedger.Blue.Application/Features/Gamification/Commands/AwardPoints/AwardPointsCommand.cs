using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Gamification.Commands.AwardPoints;

public record AwardPointsCommand(
    string CitizenEmail,
    int Points,
    string Reason
) : IRequest<AwardPointsResult>;

public record AwardPointsResult(
    bool Success,
    int TotalPoints = 0,
    int WeeklyPoints = 0,
    int MonthlyPoints = 0,
    string? Error = null);

public class AwardPointsCommandHandler : IRequestHandler<AwardPointsCommand, AwardPointsResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<AwardPointsCommandHandler> _logger;

    public AwardPointsCommandHandler(
        IMarineDbContext context,
        ILogger<AwardPointsCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AwardPointsResult> Handle(
        AwardPointsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get or create user points record
            var userPoints = await _context.UserPoints
                .FirstOrDefaultAsync(
                    p => p.CitizenEmail == request.CitizenEmail,
                    cancellationToken)
                .ConfigureAwait(false);

            if (userPoints == null)
            {
                userPoints = UserPoints.Create(request.CitizenEmail);
                _context.UserPoints.Add(userPoints);
            }

            // Add points
            userPoints.AddPoints(request.Points);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Awarded {Points} points to user {Email} for {Reason}",
                request.Points, request.CitizenEmail, request.Reason);

            return new AwardPointsResult(
                Success: true,
                TotalPoints: userPoints.TotalPoints,
                WeeklyPoints: userPoints.WeeklyPoints,
                MonthlyPoints: userPoints.MonthlyPoints);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to award points to user {Email}", request.CitizenEmail);
            return new AwardPointsResult(false, Error: ex.Message);
        }
    }
}
