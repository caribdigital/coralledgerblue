using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents an API key for authenticating API requests
/// </summary>
public class ApiKey : BaseEntity, IAuditableEntity
{
    public Guid ApiClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public string KeyPrefix { get; private set; } = string.Empty; // First 8 chars for display
    public bool IsActive { get; private set; } = true;
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    
    // Scopes (permissions) - comma-separated list
    public string Scopes { get; private set; } = string.Empty;
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation properties
    public ApiClient ApiClient { get; private set; } = null!;
    
    private ApiKey() { }
    
    public static (ApiKey apiKey, string plainKey) Create(
        Guid apiClientId,
        string name,
        DateTime? expiresAt = null,
        string scopes = "read")
    {
        var plainKey = GenerateApiKey();
        var keyHash = HashApiKey(plainKey);
        var keyPrefix = plainKey.Substring(0, 8);
        
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            ApiClientId = apiClientId,
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            IsActive = true,
            ExpiresAt = expiresAt,
            Scopes = scopes,
            CreatedAt = DateTime.UtcNow
        };
        
        return (apiKey, plainKey);
    }
    
    public void Revoke(string reason)
    {
        IsActive = false;
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason;
    }
    
    public void UpdateLastUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }
    
    public bool IsValid()
    {
        if (!IsActive) return false;
        if (RevokedAt.HasValue) return false;
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow) return false;
        
        return true;
    }
    
    public bool HasScope(string scope)
    {
        var scopes = Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return scopes.Contains(scope, StringComparer.OrdinalIgnoreCase) || scopes.Contains("admin", StringComparer.OrdinalIgnoreCase);
    }
    
    private static string GenerateApiKey()
    {
        // Generate a secure random API key: clb_ prefix + 32 random characters
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }
        
        var key = new char[32];
        for (int i = 0; i < 32; i++)
        {
            key[i] = chars[random[i] % chars.Length];
        }
        
        return $"clb_{new string(key)}";
    }
    
    private static string HashApiKey(string plainKey)
    {
        // Use SHA256 to hash the API key
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToBase64String(hashBytes);
    }
    
    public static string ComputeHash(string plainKey)
    {
        return HashApiKey(plainKey);
    }
}
