using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Custom branding configuration for a tenant
/// </summary>
public class TenantBranding : BaseEntity, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    
    // Custom Domain
    public string? CustomDomain { get; private set; }
    public bool UseCustomDomain { get; private set; } = false;
    
    // Visual Branding
    public string? LogoUrl { get; private set; }
    public string? FaviconUrl { get; private set; }
    public string? PrimaryColor { get; private set; } // Hex color
    public string? SecondaryColor { get; private set; }
    public string? AccentColor { get; private set; }
    
    // Text Branding
    public string? ApplicationTitle { get; private set; }
    public string? Tagline { get; private set; }
    public string? WelcomeMessage { get; private set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; private set; } = null!;
    
    private TenantBranding() { }
    
    public static TenantBranding Create(Guid tenantId)
    {
        var branding = new TenantBranding
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        
        return branding;
    }
    
    public void UpdateCustomDomain(string customDomain, bool useCustomDomain = true)
    {
        if (!string.IsNullOrWhiteSpace(customDomain))
        {
            CustomDomain = customDomain.ToLowerInvariant();
            UseCustomDomain = useCustomDomain;
            ModifiedAt = DateTime.UtcNow;
        }
    }
    
    public void UpdateVisualBranding(
        string? logoUrl = null,
        string? faviconUrl = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        string? accentColor = null)
    {
        if (logoUrl != null) LogoUrl = logoUrl;
        if (faviconUrl != null) FaviconUrl = faviconUrl;
        if (primaryColor != null) PrimaryColor = primaryColor;
        if (secondaryColor != null) SecondaryColor = secondaryColor;
        if (accentColor != null) AccentColor = accentColor;
        
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void UpdateTextBranding(
        string? applicationTitle = null,
        string? tagline = null,
        string? welcomeMessage = null)
    {
        if (applicationTitle != null) ApplicationTitle = applicationTitle;
        if (tagline != null) Tagline = tagline;
        if (welcomeMessage != null) WelcomeMessage = welcomeMessage;
        
        ModifiedAt = DateTime.UtcNow;
    }
}
