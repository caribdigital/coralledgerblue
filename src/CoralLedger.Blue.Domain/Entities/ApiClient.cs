using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a third-party application or organization registered to use the CoralLedger Blue API
/// </summary>
public class ApiClient : BaseEntity, IAggregateRoot, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? OrganizationName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime? DeactivatedAt { get; private set; }
    public string? DeactivationReason { get; private set; }
    
    // Rate limiting (requests per minute)
    public int RateLimitPerMinute { get; private set; } = 60;
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation properties
    public ICollection<ApiKey> ApiKeys { get; private set; } = new List<ApiKey>();
    public ICollection<ApiUsageLog> UsageLogs { get; private set; } = new List<ApiUsageLog>();
    
    private ApiClient() { }
    
    public static ApiClient Create(
        string name,
        string? organizationName = null,
        string? description = null,
        string? contactEmail = null,
        int rateLimitPerMinute = 60)
    {
        var client = new ApiClient
        {
            Id = Guid.NewGuid(),
            Name = name,
            OrganizationName = organizationName,
            Description = description,
            ContactEmail = contactEmail,
            ClientId = GenerateClientId(),
            IsActive = true,
            RateLimitPerMinute = rateLimitPerMinute,
            CreatedAt = DateTime.UtcNow
        };
        
        return client;
    }
    
    public void Deactivate(string reason)
    {
        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
        DeactivationReason = reason;
    }
    
    public void Reactivate()
    {
        IsActive = true;
        DeactivatedAt = null;
        DeactivationReason = null;
    }
    
    public void UpdateRateLimit(int requestsPerMinute)
    {
        if (requestsPerMinute <= 0)
            throw new ArgumentException("Rate limit must be greater than 0", nameof(requestsPerMinute));
            
        RateLimitPerMinute = requestsPerMinute;
    }
    
    private static string GenerateClientId()
    {
        return $"coral_{Guid.NewGuid():N}";
    }
}
