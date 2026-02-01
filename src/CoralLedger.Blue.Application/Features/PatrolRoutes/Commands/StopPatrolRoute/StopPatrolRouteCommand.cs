using CoralLedger.Blue.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.StopPatrolRoute;

public record StopPatrolRouteCommand(
    Guid PatrolRouteId,
    string? CompletionNotes = null,
    bool Cancel = false,
    string? CancellationReason = null
) : IRequest<StopPatrolRouteResult>;

public record StopPatrolRouteResult(
    bool Success,
    string Status,
    double? TotalDistanceMeters = null,
    int? DurationSeconds = null,
    string? Error = null);

public class StopPatrolRouteCommandHandler : IRequestHandler<StopPatrolRouteCommand, StopPatrolRouteResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<StopPatrolRouteCommandHandler> _logger;

    public StopPatrolRouteCommandHandler(
        IMarineDbContext context,
        ILogger<StopPatrolRouteCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<StopPatrolRouteResult> Handle(
        StopPatrolRouteCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var patrolRoute = await _context.PatrolRoutes
                .Include(p => p.Points)
                .FirstOrDefaultAsync(p => p.Id == request.PatrolRouteId, cancellationToken)
                .ConfigureAwait(false);

            if (patrolRoute == null)
            {
                return new StopPatrolRouteResult(false, string.Empty, Error: "Patrol route not found");
            }

            if (request.Cancel)
            {
                patrolRoute.Cancel(request.CancellationReason);
            }
            else
            {
                patrolRoute.Complete(request.CompletionNotes);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Stopped patrol route {Id} with status {Status}",
                patrolRoute.Id, patrolRoute.Status);

            return new StopPatrolRouteResult(
                Success: true,
                Status: patrolRoute.Status.ToString(),
                TotalDistanceMeters: patrolRoute.TotalDistanceMeters,
                DurationSeconds: patrolRoute.DurationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop patrol route {Id}", request.PatrolRouteId);
            return new StopPatrolRouteResult(false, string.Empty, Error: ex.Message);
        }
    }
}
