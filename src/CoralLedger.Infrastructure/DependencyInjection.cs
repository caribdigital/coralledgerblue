using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Infrastructure.AI;
using CoralLedger.Infrastructure.Alerts;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.ExternalServices;
using CoralLedger.Infrastructure.Jobs;
using CoralLedger.Infrastructure.Services;
using CoralLedger.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            client.Timeout = TimeSpan.FromSeconds(30); // Reduced from 5 min for better UX
        });

        // Register Protected Planet WDPA client
        services.Configure<ProtectedPlanetOptions>(
            configuration.GetSection(ProtectedPlanetOptions.SectionName));

        services.AddHttpClient<IProtectedPlanetClient, ProtectedPlanetClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Register Azure Blob Storage service
        services.Configure<BlobStorageOptions>(
            configuration.GetSection(BlobStorageOptions.SectionName));
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        // Register Marine AI service (Semantic Kernel)
        services.Configure<MarineAIOptions>(
            configuration.GetSection(MarineAIOptions.SectionName));
        services.AddScoped<IMarineAIService, MarineAIService>();
        services.AddScoped<ISpeciesClassificationService, SpeciesClassificationService>();

        // Register Alert services
        services.AddScoped<IAlertRuleEngine, AlertRuleEngine>();
        services.AddScoped<IAlertNotificationService, AlertNotificationService>();

        // Register AIS (vessel tracking) client
        services.Configure<AisOptions>(
            configuration.GetSection(AisOptions.SectionName));
        services.AddHttpClient<IAisClient, AisClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register Data Export service
        services.AddScoped<IDataExportService, DataExportService>();

        // Register Reef Health Calculator (spatial intelligence)
        services.AddScoped<IReefHealthCalculator, ReefHealthCalculator>();

        // Register MPA Proximity Service (spatial analysis)
        services.AddScoped<IMpaProximityService, MpaProximityService>();

        // Register Cache service (Redis or in-memory fallback)
        var redisOptions = configuration.GetSection(RedisCacheOptions.SectionName).Get<RedisCacheOptions>()
            ?? new RedisCacheOptions();

        // Override connection string from environment variable if set
        var connectionStringFromEnv = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(connectionStringFromEnv))
        {
            redisOptions.ConnectionString = connectionStringFromEnv;
        }

        if (redisOptions.Enabled)
        {
            // Test Redis connection before registering services
            bool redisAvailable = false;
            try
            {
                var testConnectionString = redisOptions.ConnectionString + ",connectTimeout=5000,abortConnect=false";
                using var testConnection = StackExchange.Redis.ConnectionMultiplexer.Connect(testConnectionString);
                redisAvailable = testConnection.IsConnected;
                testConnection.Close();

                if (redisAvailable)
                {
                    Console.WriteLine($"Redis connection successful: {redisOptions.ConnectionString}");
                }
            }
            catch (Exception ex)
            {
                // Redis is not available, will fall back to in-memory cache
                Console.WriteLine($"Redis connection failed: {ex.Message}. Falling back to in-memory cache.");
                redisAvailable = false;
            }

            if (redisAvailable)
            {
                // Redis is available, register Redis cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisOptions.ConnectionString;
                    options.InstanceName = redisOptions.InstanceName;
                });

                // Register Redis connection multiplexer for advanced operations (prefix-based removal)
                services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                {
                    return StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
                });

                services.AddSingleton<ICacheService, RedisCacheService>();
            }
            else
            {
                // Redis connection test failed, fall back to in-memory cache
                Console.WriteLine("Using in-memory cache as fallback.");
                AddInMemoryCache(services);
            }
        }
        else
        {
            // Use in-memory cache when Redis is disabled
            Console.WriteLine("Redis disabled in configuration. Using in-memory cache.");
            AddInMemoryCache(services);
        }

        services.Configure<RedisCacheOptions>(
            configuration.GetSection(RedisCacheOptions.SectionName));

        // Register Metrics
        services.AddSingleton<MarineMetrics>();

        return services;
    }

    /// <summary>
    /// Registers in-memory cache as fallback when Redis is unavailable or disabled
    /// </summary>
    private static void AddInMemoryCache(IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
    }

    /// <summary>
    /// Add health checks for all infrastructure services
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        // Add HTTP client for health checks that need to make HTTP requests
        services.AddHttpClient("HealthChecks", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddHealthChecks()
            // Infrastructure health checks
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready", "db" })
            .AddCheck<NoaaHealthCheck>("noaa-api", tags: new[] { "ready", "external" })
            .AddCheck<GfwHealthCheck>("gfw-api", tags: new[] { "ready", "external" })
            .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: new[] { "ready", "storage" })
            .AddCheck<QuartzHealthCheck>("quartz-scheduler", tags: new[] { "ready", "jobs" })
            // Frontend and connectivity health checks
            .AddCheck<BlazorHealthCheck>("blazor", tags: new[] { "ready", "frontend" })
            .AddCheck<SignalRHealthCheck>("signalr", tags: new[] { "ready", "realtime" })
            .AddCheck<CacheHealthCheck>("cache", tags: new[] { "ready", "performance" });

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
