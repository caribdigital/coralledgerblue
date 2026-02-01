using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Infrastructure.Services;

public class ApiUsageService : IApiUsageService
{
    private readonly MarineDbContext _context;

    public ApiUsageService(MarineDbContext context)
    {
        _context = context;
    }

    public async Task LogApiUsageAsync(
        Guid apiClientId,
        Guid? apiKeyId,
        string endpoint,
        string httpMethod,
        int statusCode,
        int responseTimeMs,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var log = ApiUsageLog.Create(
            apiClientId,
            apiKeyId,
            endpoint,
            httpMethod,
            statusCode,
            responseTimeMs,
            ipAddress,
            userAgent,
            errorMessage);

        _context.ApiUsageLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ApiUsageStatistics> GetUsageStatisticsAsync(
        Guid apiClientId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        startDate ??= DateTime.UtcNow.AddDays(-30);
        endDate ??= DateTime.UtcNow;

        var logs = await _context.ApiUsageLogs
            .Where(l => l.ApiClientId == apiClientId && 
                       l.Timestamp >= startDate && 
                       l.Timestamp <= endDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var statistics = new ApiUsageStatistics
        {
            ApiClientId = apiClientId,
            TotalRequests = logs.Count,
            SuccessfulRequests = logs.Count(l => l.StatusCode >= 200 && l.StatusCode < 400),
            FailedRequests = logs.Count(l => l.StatusCode >= 400),
            AverageResponseTimeMs = logs.Any() ? logs.Average(l => l.ResponseTimeMs) : 0,
            RequestsByEndpoint = logs
                .GroupBy(l => l.Endpoint)
                .ToDictionary(g => g.Key, g => g.Count()),
            RequestsByStatusCode = logs
                .GroupBy(l => l.StatusCode)
                .ToDictionary(g => g.Key, g => g.Count()),
            FirstRequest = logs.Any() ? logs.Min(l => l.Timestamp) : null,
            LastRequest = logs.Any() ? logs.Max(l => l.Timestamp) : null
        };

        return statistics;
    }

    public async Task<List<ApiUsageLog>> GetUsageLogsAsync(
        Guid apiClientId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.ApiUsageLogs
            .Where(l => l.ApiClientId == apiClientId)
            .OrderByDescending(l => l.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
