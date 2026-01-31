using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace CoralLedger.Blue.Web.Endpoints;

/// <summary>
/// Diagnostic endpoints for system health and readiness checks
/// </summary>
public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/diagnostics")
            .WithTags("Diagnostics");

        // GET /api/diagnostics/ready - Comprehensive readiness check
        group.MapGet("/ready", async (
            HealthCheckService healthCheckService,
            CancellationToken ct) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var report = await healthCheckService.CheckHealthAsync(
                r => r.Tags.Contains("ready"), ct).ConfigureAwait(false);
            stopwatch.Stop();

            var response = new ReadinessResponse
            {
                Status = report.Status.ToString().ToLowerInvariant(),
                TotalDurationMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow,
                Checks = report.Entries.ToDictionary(
                    e => e.Key,
                    e => new HealthCheckDetail
                    {
                        Status = e.Value.Status.ToString().ToLowerInvariant(),
                        Description = e.Value.Description,
                        DurationMs = e.Value.Duration.TotalMilliseconds,
                        Data = e.Value.Data.ToDictionary(d => d.Key, d => d.Value?.ToString()),
                        Exception = e.Value.Exception?.Message
                    })
            };

            var statusCode = report.Status switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status200OK,
                HealthStatus.Unhealthy => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status200OK
            };

            return Results.Json(response, statusCode: statusCode);
        })
        .WithName("GetReadiness")
        .WithDescription("Comprehensive readiness check combining all health checks")
        .Produces<ReadinessResponse>()
        .Produces<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable);

        // GET /api/diagnostics/info - System information
        group.MapGet("/info", () =>
        {
            return Results.Ok(new
            {
                application = "CoralLedger Blue",
                version = typeof(DiagnosticsEndpoints).Assembly
                    .GetName().Version?.ToString() ?? "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                runtime = new
                {
                    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
                },
                timestamp = DateTime.UtcNow
            });
        })
        .WithName("GetSystemInfo")
        .WithDescription("Get system and runtime information");

        // GET /api/diagnostics/checks - List all registered health checks
        group.MapGet("/checks", (
            HealthCheckService healthCheckService) =>
        {
            // Note: HealthCheckService doesn't expose registered checks directly
            // We return information about the check structure
            return Results.Ok(new
            {
                description = "Health checks are registered with the 'ready' tag",
                availableEndpoints = new[]
                {
                    new { path = "/api/diagnostics/ready", description = "Run all readiness checks" },
                    new { path = "/api/diagnostics/info", description = "System information" },
                    new { path = "/health", description = "Basic health probe (development only)" },
                    new { path = "/alive", description = "Liveness probe (development only)" }
                },
                healthCheckCategories = new[]
                {
                    new { tag = "db", description = "Database connectivity" },
                    new { tag = "external", description = "External API dependencies" },
                    new { tag = "storage", description = "Storage services" },
                    new { tag = "jobs", description = "Background job scheduler" },
                    new { tag = "frontend", description = "Frontend assets (Blazor)" },
                    new { tag = "realtime", description = "Real-time services (SignalR)" },
                    new { tag = "performance", description = "Performance services (Cache)" }
                }
            });
        })
        .WithName("GetHealthCheckInfo")
        .WithDescription("List available health check endpoints and categories");

        return endpoints;
    }
}

/// <summary>
/// Response model for readiness check endpoint
/// </summary>
public class ReadinessResponse
{
    public string Status { get; set; } = "";
    public long TotalDurationMs { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, HealthCheckDetail> Checks { get; set; } = new();
}

/// <summary>
/// Detail model for individual health check results
/// </summary>
public class HealthCheckDetail
{
    public string Status { get; set; } = "";
    public string? Description { get; set; }
    public double DurationMs { get; set; }
    public Dictionary<string, string?> Data { get; set; } = new();
    public string? Exception { get; set; }
}
