using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a password reset token for resetting user passwords
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    
    // Navigation property
    public TenantUser User { get; private set; } = null!;
    
    private PasswordResetToken() { }
    
    public static PasswordResetToken Create(Guid userId, int expirationHours = 2)
    {
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        
        return token;
    }
    
    public bool IsValid()
    {
        return !IsUsed && DateTime.UtcNow < ExpiresAt;
    }
    
    public void MarkAsUsed()
    {
        if (IsUsed)
            throw new InvalidOperationException("Token has already been used");
        
        // Defensive check - this should already be validated by IsValid() before calling MarkAsUsed()
        if (DateTime.UtcNow >= ExpiresAt)
            throw new InvalidOperationException("Token has expired");
            
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }
    
    private static string GenerateSecureToken()
    {
        // Generate a 32-byte random token and convert to Base64 URL-safe string
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        
        // Convert to Base64 URL-safe format (replace +/= with -_)
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
