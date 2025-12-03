using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.ExternalServices;
using CoralLedger.Infrastructure.Jobs;
using CoralLedger.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace CoralLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddSingleton<ISpatialValidationService, SpatialValidationService>();

        // Register Global Fishing Watch client
        services.Configure<GlobalFishingWatchOptions>(
            configuration.GetSection(GlobalFishingWatchOptions.SectionName));

        services.AddHttpClient<IGlobalFishingWatchClient, GlobalFishingWatchClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Register NOAA Coral Reef Watch client
        services.AddHttpClient<ICoralReefWatchClient, CoralReefWatchClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // Register Protected Planet WDPA client
        services.Configure<ProtectedPlanetOptions>(
            configuration.GetSection(ProtectedPlanetOptions.SectionName));

        services.AddHttpClient<IProtectedPlanetClient, ProtectedPlanetClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        return services;
    }

    public static void AddMarineDatabase(
        this IHostApplicationBuilder builder,
        string connectionName)
    {
        // Aspire's AddNpgsqlDbContext handles the connection, but we need to configure NTS
        // Using the configureDbContextOptions callback to add NetTopologySuite support
        builder.AddNpgsqlDbContext<MarineDbContext>(connectionName,
            configureDbContextOptions: options =>
            {
                // Configure Npgsql to use NetTopologySuite for spatial types
                // Note: This is a workaround as Aspire's API doesn't expose configureDataSourceBuilder
                options.UseNpgsql(npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                });
            });

        builder.Services.AddScoped<IMarineDbContext>(sp =>
            sp.GetRequiredService<MarineDbContext>());
    }

    /// <summary>
    /// Add Quartz.NET background job scheduler with configured jobs
    /// </summary>
    public static IServiceCollection AddQuartzJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            // BleachingDataSyncJob - syncs NOAA bleaching data for all MPAs
            q.AddJob<BleachingDataSyncJob>(opts => opts
                .WithIdentity(BleachingDataSyncJob.Key)
                .StoreDurably());

            q.AddTrigger(opts => opts
                .ForJob(BleachingDataSyncJob.Key)
                .WithIdentity("BleachingDataSyncJob-DailyTrigger")
                .WithDescription("Runs daily at 6 AM UTC to sync NOAA bleaching data")
                .WithCronSchedule("0 0 6 * * ?") // 6:00 AM UTC daily
                .StartNow()); // Also run immediately on startup

            // VesselEventSyncJob - syncs GFW fishing events for Bahamas
            q.AddJob<VesselEventSyncJob>(opts => opts
                .WithIdentity(VesselEventSyncJob.Key)
                .StoreDurably());

            q.AddTrigger(opts => opts
                .ForJob(VesselEventSyncJob.Key)
                .WithIdentity("VesselEventSyncJob-6HourTrigger")
                .WithDescription("Runs every 6 hours to sync GFW fishing events for Bahamas")
                .WithCronSchedule("0 0 */6 * * ?") // Every 6 hours
                .StartNow()); // Also run immediately on startup
        });

        // Add Quartz as a hosted service
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
