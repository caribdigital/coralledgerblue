using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.StartPatrolRoute;

public record StartPatrolRouteCommand(
    string? OfficerName = null,
    string? OfficerId = null,
    string? Notes = null,
    int RecordingIntervalSeconds = 30
) : IRequest<StartPatrolRouteResult>;

public record StartPatrolRouteResult(
    bool Success,
    Guid? PatrolRouteId = null,
    string? Error = null);

public class StartPatrolRouteCommandHandler : IRequestHandler<StartPatrolRouteCommand, StartPatrolRouteResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<StartPatrolRouteCommandHandler> _logger;

    public StartPatrolRouteCommandHandler(
        IMarineDbContext context,
        ILogger<StartPatrolRouteCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<StartPatrolRouteResult> Handle(
        StartPatrolRouteCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var patrolRoute = PatrolRoute.Create(
                request.OfficerName,
                request.OfficerId,
                request.Notes,
                request.RecordingIntervalSeconds);

            _context.PatrolRoutes.Add(patrolRoute);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Started patrol route {Id} for officer {OfficerName}",
                patrolRoute.Id, request.OfficerName ?? "Unknown");

            return new StartPatrolRouteResult(
                Success: true,
                PatrolRouteId: patrolRoute.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start patrol route");
            return new StartPatrolRouteResult(false, Error: ex.Message);
        }
    }
}
