using System.Security.Cryptography;
using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a refresh token for obtaining new access tokens
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid TenantUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    
    // Navigation property
    public TenantUser User { get; private set; } = null!;
    
    private RefreshToken() { }
    
    public static RefreshToken Create(Guid tenantUserId, string token, int expirationDays = 30)
    {
        var tokenHash = HashToken(token);
        
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantUserId = tenantUserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow
        };
        
        return refreshToken;
    }
    
    public bool IsValid()
    {
        return RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
    
    public void Revoke(Guid? replacedByTokenId = null)
    {
        if (RevokedAt.HasValue)
            throw new InvalidOperationException("Token has already been revoked");
            
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
    
    public static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hashBytes = sha256.ComputeHash(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }
    
    public bool VerifyToken(string token)
    {
        var tokenHash = HashToken(token);
        return TokenHash == tokenHash;
    }
}
