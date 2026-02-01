using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Infrastructure.AI;
using CoralLedger.Blue.Infrastructure.Alerts;
using CoralLedger.Blue.Infrastructure.Data;
using CoralLedger.Blue.Infrastructure.ExternalServices;
using CoralLedger.Blue.Infrastructure.Jobs;
using CoralLedger.Blue.Infrastructure.Services;
using CoralLedger.Blue.Infrastructure.Services.PatrolExport;
using CoralLedger.Blue.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CoralLedger.Blue.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddSingleton<ISpatialValidationService, SpatialValidationService>();
        services.AddSingleton<ISpatialCalculator, SpatialCalculator>();

        // Register Global Fishing Watch client
        services.Configure<GlobalFishingWatchOptions>(
            configuration.GetSection(GlobalFishingWatchOptions.SectionName));

        services.AddHttpClient<IGlobalFishingWatchClient, GlobalFishingWatchClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Register NOAA Coral Reef Watch client
        services.Configure<CoralReefWatchOptions>(
            configuration.GetSection(CoralReefWatchOptions.SectionName));

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

        // Sprint 5.2.5: Register Semantic Search service (vector embeddings)
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();

        // Register Email service (SendGrid)
        services.Configure<SendGridOptions>(
            configuration.GetSection(SendGridOptions.SectionName));
        services.AddSingleton<IEmailService, SendGridEmailService>();

        // Register Web Push notification service
        services.Configure<WebPushOptions>(
            configuration.GetSection(WebPushOptions.SectionName));
        services.AddSingleton<IPushNotificationService, WebPushNotificationService>();

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

        // Register PDF Report Generation service
        services.AddScoped<IReportGenerationService, PdfReportGenerationService>();

        // Register Reef Health Calculator (spatial intelligence)
        services.AddScoped<IReefHealthCalculator, ReefHealthCalculator>();

        // Register MPA Proximity Service (spatial analysis)
        services.AddScoped<IMpaProximityService, MpaProximityService>();

        // Register Batch Containment Service (optimized point-in-polygon queries)
        // Sprint 3.3 US-3.3.2: <100ms for 10K positions
        services.AddScoped<IBatchContainmentService, BatchContainmentService>();

        // Register Observation Validation Service (citizen science validation)
        // Sprint 4.2 US-4.2.2/4.2.3/4.2.6: EXIF, geofencing, plausibility checks
        services.AddScoped<IObservationValidationService, ObservationValidationService>();

        // Register Patrol Route Export Service
        services.AddScoped<IPatrolRouteExportService, PatrolRouteExportService>();

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
        // Get the connection string from Aspire's configuration (injected by AppHost via WithReference)
        // Aspire stores connection strings in the format: ConnectionStrings__<connectionName>
        var connectionString = builder.Configuration.GetConnectionString(connectionName);

        // Register the DbContext with Npgsql + NetTopologySuite support.
        // We use AddDbContext (not AddNpgsqlDbContext) to have full control over UseNpgsql configuration.
        // See: https://github.com/dotnet/aspire/issues/7746
        builder.Services.AddDbContext<MarineDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Enable NetTopologySuite for spatial types (PostGIS geometry/geography)
                npgsqlOptions.UseNetTopologySuite();

                // Configure retry policy for transient failures (matching Aspire defaults)
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
        });

        // Enrich with Aspire's health checks, logging, and telemetry
        // This provides the same observability features as AddNpgsqlDbContext
        builder.EnrichNpgsqlDbContext<MarineDbContext>();

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

            // ScheduledReportJob - Weekly Report
            // Configure recipients in appsettings.json or via environment variable:
            // "Quartz": { "JobDataMap": { "WeeklyReportJob:Recipients": "user1@example.com,user2@example.com" } }
            q.AddJob<ScheduledReportJob>(opts => opts
                .WithIdentity(ScheduledReportJob.WeeklyReportKey)
                .UsingJobData("Recipients", "") // Recipients configured via appsettings.json or environment
                .StoreDurably());

            q.AddTrigger(opts => opts
                .ForJob(ScheduledReportJob.WeeklyReportKey)
                .WithIdentity("WeeklyReportJob-Trigger")
                .WithDescription("Runs every Monday at 8 AM UTC to generate weekly MPA summary report")
                .WithCronSchedule("0 0 8 ? * MON")); // Every Monday at 8:00 AM UTC

            // ScheduledReportJob - Monthly Report
            // Configure recipients in appsettings.json or via environment variable:
            // "Quartz": { "JobDataMap": { "MonthlyReportJob:Recipients": "user1@example.com,user2@example.com" } }
            q.AddJob<ScheduledReportJob>(opts => opts
                .WithIdentity(ScheduledReportJob.MonthlyReportKey)
                .UsingJobData("Recipients", "") // Recipients configured via appsettings.json or environment
                .StoreDurably());

            q.AddTrigger(opts => opts
                .ForJob(ScheduledReportJob.MonthlyReportKey)
                .WithIdentity("MonthlyReportJob-Trigger")
                .WithDescription("Runs on the 1st of each month at 8 AM UTC to generate monthly MPA summary report")
                .WithCronSchedule("0 0 8 1 * ?")); // 1st of every month at 8:00 AM UTC
        });

        // Add Quartz as a hosted service
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
