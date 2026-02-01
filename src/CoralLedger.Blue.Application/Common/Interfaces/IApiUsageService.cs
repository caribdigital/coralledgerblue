using CoralLedger.Blue.Domain.Entities;

namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Service for tracking API usage analytics
/// </summary>
public interface IApiUsageService
{
    /// <summary>
    /// Logs an API request
    /// </summary>
    Task LogApiUsageAsync(
        Guid apiClientId,
        Guid? apiKeyId,
        string endpoint,
        string httpMethod,
        int statusCode,
        int responseTimeMs,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets usage statistics for an API client
    /// </summary>
    Task<ApiUsageStatistics> GetUsageStatisticsAsync(
        Guid apiClientId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets usage logs for an API client
    /// </summary>
    Task<List<ApiUsageLog>> GetUsageLogsAsync(
        Guid apiClientId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// API usage statistics
/// </summary>
public class ApiUsageStatistics
{
    public Guid ApiClientId { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public Dictionary<string, int> RequestsByEndpoint { get; set; } = new();
    public Dictionary<int, int> RequestsByStatusCode { get; set; } = new();
    public DateTime? FirstRequest { get; set; }
    public DateTime? LastRequest { get; set; }
}
