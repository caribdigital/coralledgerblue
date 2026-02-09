using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents a user within a specific tenant with role-based access
/// </summary>
public class TenantUser : BaseEntity, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? FullName { get; private set; }
    public string? PasswordHash { get; private set; }
    public string? OAuthProvider { get; private set; } // Google, Microsoft, null for local auth
    public string? OAuthSubjectId { get; private set; } // OAuth provider's unique user ID
    public string Role { get; private set; } = "User"; // Admin, Manager, User, Viewer
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedOutUntil { get; private set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; private set; } = null!;
    
    private TenantUser() { }
    
    public static TenantUser Create(
        Guid tenantId,
        string email,
        string? fullName = null,
        string role = "User")
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
            
        var user = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.ToLowerInvariant(),
            FullName = fullName,
            Role = role,
            IsActive = true,
            EmailConfirmed = false,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        
        return user;
    }
    
    public void SetPassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        
        // Lock account after 5 failed attempts for 15 minutes
        if (FailedLoginAttempts >= 5)
        {
            LockedOutUntil = DateTime.UtcNow.AddMinutes(15);
        }
        
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void ResetFailedLoginAttempts()
    {
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public bool IsLockedOut()
    {
        return LockedOutUntil.HasValue && LockedOutUntil.Value > DateTime.UtcNow;
    }
    
    public void UpdateRole(string role)
    {
        Role = role;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void Reactivate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        ResetFailedLoginAttempts();
    }
    
    public void SetOAuthProvider(string provider, string subjectId)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required", nameof(provider));
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("Subject ID is required", nameof(subjectId));

        OAuthProvider = provider;
        OAuthSubjectId = subjectId;
        ModifiedAt = DateTime.UtcNow;
    }

    // Two-Factor Authentication
    public bool TwoFactorEnabled { get; private set; }
    public string? TwoFactorSecretKey { get; private set; }

    public void EnableTwoFactor(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key is required", nameof(secretKey));

        TwoFactorSecretKey = secretKey;
        TwoFactorEnabled = true;
        ModifiedAt = DateTime.UtcNow;
    }

    public void DisableTwoFactor()
    {
        TwoFactorSecretKey = null;
        TwoFactorEnabled = false;
        ModifiedAt = DateTime.UtcNow;
    }
}
