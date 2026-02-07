using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Hosting;

namespace CoralLedger.Blue.Web.Security;

/// <summary>
/// Security configuration for rate limiting, CORS, and security headers
/// </summary>
public static class SecurityConfiguration
{
    public const string DefaultRateLimiterPolicy = "default";
    public const string ApiRateLimiterPolicy = "api";
    public const string StrictRateLimiterPolicy = "strict";
    public const string EmailRateLimiterPolicy = "email";

    /// <summary>
    /// Configure rate limiting policies
    /// </summary>
    public static IServiceCollection AddSecurityRateLimiting(this IServiceCollection services, IHostEnvironment? environment = null)
    {
        // Use relaxed limits in Testing environment to avoid test interference
        var isTesting = environment?.EnvironmentName == "Testing";

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Default policy: 100 requests per minute per IP
            options.AddPolicy(DefaultRateLimiterPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            // API policy: 60 requests per minute per IP (for data-heavy endpoints)
            options.AddPolicy(ApiRateLimiterPolicy, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 60,
                        TokensPerPeriod = 60,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    }));

            // Strict policy: 10 requests per minute (for auth/admin endpoints)
            options.AddPolicy(StrictRateLimiterPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));

            // Email policy: 3 requests per 15 minutes per IP (for email sending endpoints)
            // Relaxed to 100 in Testing environment to avoid test interference
            options.AddPolicy(EmailRateLimiterPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isTesting ? 100 : 3,
                        Window = TimeSpan.FromMinutes(isTesting ? 1 : 15),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Global limiter as fallback
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? retry.TotalSeconds
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString("F0");

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too Many Requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfterSeconds = retryAfter
                }, token);
            };
        });

        return services;
    }

    /// <summary>
    /// Configure CORS policies
    /// </summary>
    public static IServiceCollection AddSecurityCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
            ?? new[] { "https://localhost:5001" };

        services.AddCors(options =>
        {
            // Default policy for web app
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });

            // Strict policy for API
            options.AddPolicy("ApiPolicy", builder =>
            {
                builder
                    .WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Content-Type", "Authorization", "X-Requested-With")
                    .AllowCredentials();
            });

            // Public policy for public data (read-only)
            options.AddPolicy("PublicApi", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .WithMethods("GET")
                    .WithHeaders("Content-Type");
            });
        });

        return services;
    }

    /// <summary>
    /// Add security headers middleware
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // Prevent clickjacking
            headers["X-Frame-Options"] = "DENY";

            // Prevent MIME type sniffing
            headers["X-Content-Type-Options"] = "nosniff";

            // Enable XSS protection
            headers["X-XSS-Protection"] = "1; mode=block";

            // Referrer policy
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Permissions policy (restrict powerful features)
            headers["Permissions-Policy"] = "geolocation=(self), camera=(self), microphone=()";

            // Content Security Policy (adjust as needed for Blazor)
            if (!context.Request.Path.StartsWithSegments("/_blazor"))
            {
                headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                    "img-src 'self' data: https:; " +
                    "font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                    "connect-src 'self' wss: https:; " +
                    "frame-ancestors 'none';";
            }

            await next();
        });
    }

    /// <summary>
    /// Configure response compression
    /// </summary>
    public static IServiceCollection AddPerformanceCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/json", "application/geo+json", "text/csv" });
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.AddResponseCaching();

        return services;
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check for forwarded IP (behind load balancer/proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
