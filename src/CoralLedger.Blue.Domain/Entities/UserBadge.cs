using CoralLedger.Blue.Domain.Common;
using CoralLedger.Blue.Domain.Enums;

namespace CoralLedger.Blue.Domain.Entities;

public class UserBadge : BaseEntity, IAuditableEntity
{
    public string CitizenEmail { get; private set; } = string.Empty;
    public Guid? TenantUserId { get; private set; } // Link to authenticated user
    public BadgeType BadgeType { get; private set; }
    public DateTime EarnedAt { get; private set; }
    public string? Description { get; private set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation property
    public TenantUser? TenantUser { get; private set; }

    private UserBadge() { }

    public static UserBadge Create(
        string citizenEmail,
        BadgeType badgeType,
        string? description = null,
        Guid? tenantUserId = null)
    {
        if (string.IsNullOrWhiteSpace(citizenEmail))
            throw new ArgumentException("Citizen email is required", nameof(citizenEmail));

        return new UserBadge
        {
            Id = Guid.NewGuid(),
            CitizenEmail = citizenEmail,
            TenantUserId = tenantUserId,
            BadgeType = badgeType,
            EarnedAt = DateTime.UtcNow,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void LinkToUser(Guid tenantUserId)
    {
        TenantUserId = tenantUserId;
        ModifiedAt = DateTime.UtcNow;
    }
}
