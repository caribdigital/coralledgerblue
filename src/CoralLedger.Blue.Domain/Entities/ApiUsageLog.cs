using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a log entry for API usage tracking and analytics
/// </summary>
public class ApiUsageLog : BaseEntity
{
    public Guid ApiClientId { get; private set; }
    public Guid? ApiKeyId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string HttpMethod { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public int ResponseTimeMs { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? ErrorMessage { get; private set; }
    
    // Navigation properties
    public ApiClient ApiClient { get; private set; } = null!;
    public ApiKey? ApiKey { get; private set; }
    
    private ApiUsageLog() { }
    
    public static ApiUsageLog Create(
        Guid apiClientId,
        Guid? apiKeyId,
        string endpoint,
        string httpMethod,
        int statusCode,
        int responseTimeMs,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null)
    {
        return new ApiUsageLog
        {
            Id = Guid.NewGuid(),
            ApiClientId = apiClientId,
            ApiKeyId = apiKeyId,
            Timestamp = DateTime.UtcNow,
            Endpoint = endpoint,
            HttpMethod = httpMethod,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ErrorMessage = errorMessage
        };
    }
}
