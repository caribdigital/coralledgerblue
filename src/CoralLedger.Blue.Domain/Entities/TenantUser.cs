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
    public string Role { get; private set; } = "User"; // Admin, Manager, User, Viewer
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }
    
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
            CreatedAt = DateTime.UtcNow
        };
        
        return user;
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
    }
}
