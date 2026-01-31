using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Quartz;

namespace CoralLedger.Blue.Infrastructure.Jobs;

/// <summary>
/// Scheduled job that fetches coral bleaching data from NOAA Coral Reef Watch
/// for all Marine Protected Areas and persists to database for historical analysis.
/// </summary>
[DisallowConcurrentExecution]
public class BleachingDataSyncJob : IJob
{
    public static readonly JobKey Key = new("BleachingDataSyncJob", "DataSync");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BleachingDataSyncJob> _logger;

    public BleachingDataSyncJob(
        IServiceScopeFactory scopeFactory,
        ILogger<BleachingDataSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting BleachingDataSyncJob at {Time}", DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        var crwClient = scope.ServiceProvider.GetRequiredService<ICoralReefWatchClient>();

        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)); // Yesterday's data
        var successCount = 0;
        var failCount = 0;

        try
        {
            // Get all MPAs with their centroids
            var mpas = await dbContext.MarineProtectedAreas
                .Select(m => new { m.Id, m.Name, m.Centroid })
                .ToListAsync(context.CancellationToken);

            _logger.LogInformation("Syncing bleaching data for {Count} MPAs for date {Date}",
                mpas.Count, targetDate);

            // Process each MPA with rate limiting
            var semaphore = new SemaphoreSlim(3); // Max 3 concurrent NOAA requests

            var tasks = mpas.Select(async mpa =>
            {
                await semaphore.WaitAsync(context.CancellationToken);
                try
                {
                    await ProcessMpaAsync(dbContext, crwClient, mpa.Id, mpa.Name, mpa.Centroid, targetDate, context.CancellationToken);
                    Interlocked.Increment(ref successCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failCount);
                    _logger.LogWarning(ex, "Failed to sync bleaching data for MPA {MpaName} ({MpaId})",
                        mpa.Name, mpa.Id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Save all changes
            await dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "BleachingDataSyncJob completed. Success: {Success}, Failed: {Failed}",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BleachingDataSyncJob failed with critical error");
            throw;
        }
    }

    private async Task ProcessMpaAsync(
        MarineDbContext dbContext,
        ICoralReefWatchClient crwClient,
        Guid mpaId,
        string mpaName,
        Point centroid,
        DateOnly targetDate,
        CancellationToken ct)
    {
        // Fetch data from NOAA
        var result = await crwClient.GetBleachingDataAsync(
            centroid.X, // Longitude
            centroid.Y, // Latitude
            targetDate,
            ct);

        if (!result.Success)
        {
            _logger.LogWarning("Failed to fetch bleaching data for {MpaName} at ({Lon}, {Lat}): {Error}",
                mpaName, centroid.X, centroid.Y, result.ErrorMessage);
            return;
        }

        var bleachingData = result.Value;

        if (bleachingData is null)
        {
            _logger.LogWarning("No bleaching data available for {MpaName} at ({Lon}, {Lat}) on {Date}",
                mpaName, centroid.X, centroid.Y, targetDate);
            return;
        }

        // Check for existing record
        var existing = await dbContext.BleachingAlerts
            .FirstOrDefaultAsync(a =>
                a.MarineProtectedAreaId == mpaId &&
                a.Date == targetDate, ct);

        if (existing is not null)
        {
            // Update existing record
            existing.UpdateMetrics(
                bleachingData.SeaSurfaceTemperature,
                bleachingData.SstAnomaly,
                bleachingData.DegreeHeatingWeek,
                bleachingData.HotSpot);

            _logger.LogDebug("Updated bleaching data for {MpaName}: DHW={Dhw}, SST={Sst}",
                mpaName, bleachingData.DegreeHeatingWeek, bleachingData.SeaSurfaceTemperature);
        }
        else
        {
            // Create new record
            var alert = BleachingAlert.Create(
                location: centroid,
                date: targetDate,
                sst: bleachingData.SeaSurfaceTemperature,
                sstAnomaly: bleachingData.SstAnomaly,
                dhw: bleachingData.DegreeHeatingWeek,
                hotSpot: bleachingData.HotSpot,
                mpaId: mpaId);

            dbContext.BleachingAlerts.Add(alert);

            _logger.LogDebug("Created bleaching alert for {MpaName}: DHW={Dhw}, SST={Sst}, Alert={Alert}",
                mpaName, bleachingData.DegreeHeatingWeek, bleachingData.SeaSurfaceTemperature, alert.AlertLevel);
        }
    }
}
