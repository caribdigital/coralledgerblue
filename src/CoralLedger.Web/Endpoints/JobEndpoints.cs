using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace CoralLedger.Web.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/jobs")
            .WithTags("Background Jobs");

        // GET /api/jobs/status - Get job status and last sync info
        group.MapGet("/status", async (
            ISchedulerFactory schedulerFactory,
            IMarineDbContext dbContext,
            CancellationToken ct = default) =>
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);

            // Get bleaching job info
            var bleachingTriggers = await scheduler.GetTriggersOfJob(BleachingDataSyncJob.Key, ct);
            var bleachingTrigger = bleachingTriggers.FirstOrDefault();

            // Get last sync from database
            var lastBleachingSync = await dbContext.BleachingAlerts
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new { b.CreatedAt, b.Date })
                .FirstOrDefaultAsync(ct);

            // Count records by date
            var recordsByDate = await dbContext.BleachingAlerts
                .GroupBy(b => b.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Date)
                .Take(7)
                .ToListAsync(ct);

            return Results.Ok(new JobStatusResponse
            {
                BleachingSync = new JobInfo
                {
                    JobName = "BleachingDataSyncJob",
                    NextFireTime = bleachingTrigger?.GetNextFireTimeUtc()?.UtcDateTime,
                    PreviousFireTime = bleachingTrigger?.GetPreviousFireTimeUtc()?.UtcDateTime,
                    LastDataDate = lastBleachingSync?.Date,
                    LastSyncTime = lastBleachingSync?.CreatedAt,
                    TotalRecords = await dbContext.BleachingAlerts.CountAsync(ct)
                },
                RecentSyncs = recordsByDate.Select(r => new SyncRecord
                {
                    Date = r.Date,
                    RecordCount = r.Count
                }).ToList()
            });
        })
        .WithName("GetJobStatus")
        .WithDescription("Get status of background data sync jobs")
        .Produces<JobStatusResponse>();

        // POST /api/jobs/sync/bleaching - Trigger manual bleaching sync
        group.MapPost("/sync/bleaching", async (
            ISchedulerFactory schedulerFactory,
            CancellationToken ct = default) =>
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);

            // Check if job is already running
            var runningJobs = await scheduler.GetCurrentlyExecutingJobs(ct);
            if (runningJobs.Any(j => j.JobDetail.Key.Equals(BleachingDataSyncJob.Key)))
            {
                return Results.Conflict(new { Message = "Bleaching sync job is already running" });
            }

            // Trigger the job immediately
            await scheduler.TriggerJob(BleachingDataSyncJob.Key, ct);

            return Results.Accepted(value: new
            {
                Message = "Bleaching sync job triggered successfully",
                TriggeredAt = DateTime.UtcNow
            });
        })
        .WithName("TriggerBleachingSync")
        .WithDescription("Manually trigger bleaching data sync from NOAA")
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }
}

public record JobStatusResponse
{
    public JobInfo BleachingSync { get; init; } = new();
    public List<SyncRecord> RecentSyncs { get; init; } = new();
}

public record JobInfo
{
    public string JobName { get; init; } = string.Empty;
    public DateTime? NextFireTime { get; init; }
    public DateTime? PreviousFireTime { get; init; }
    public DateOnly? LastDataDate { get; init; }
    public DateTime? LastSyncTime { get; init; }
    public int TotalRecords { get; init; }
}

public record SyncRecord
{
    public DateOnly Date { get; init; }
    public int RecordCount { get; init; }
}
