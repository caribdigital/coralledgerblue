using System.Diagnostics;
using System.Security.Claims;
using CoralLedger.Blue.Application.Common.Interfaces;

namespace CoralLedger.Blue.Web.Security;

/// <summary>
/// Middleware to track API usage for authenticated requests
/// </summary>
public class ApiUsageTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiUsageTrackingMiddleware> _logger;

    public ApiUsageTrackingMiddleware(
        RequestDelegate next,
        ILogger<ApiUsageTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiUsageService apiUsageService)
    {
        // Only track API endpoints
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Check if request is authenticated with API key
        var apiClientIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier);
        var apiKeyIdClaim = context.User?.FindFirst("ApiKeyId");

        if (apiClientIdClaim == null || !Guid.TryParse(apiClientIdClaim.Value, out var apiClientId))
        {
            await _next(context);
            return;
        }

        Guid? apiKeyId = null;
        if (apiKeyIdClaim != null && Guid.TryParse(apiKeyIdClaim.Value, out var keyId))
        {
            apiKeyId = keyId;
        }

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log usage asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await apiUsageService.LogApiUsageAsync(
                        apiClientId,
                        apiKeyId,
                        context.Request.Path,
                        context.Request.Method,
                        context.Response.StatusCode,
                        (int)stopwatch.ElapsedMilliseconds,
                        GetClientIpAddress(context),
                        context.Request.Headers["User-Agent"].FirstOrDefault(),
                        context.Response.StatusCode >= 400 ? $"Status: {context.Response.StatusCode}" : null,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log API usage");
                }
            });
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
