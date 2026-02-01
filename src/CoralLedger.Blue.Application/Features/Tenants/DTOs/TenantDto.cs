namespace CoralLedger.Blue.Application.Features.Tenants.DTOs;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RegionCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public TenantConfigurationDto? Configuration { get; set; }
    public TenantBrandingDto? Branding { get; set; }
}

public class TenantConfigurationDto
{
    public bool EnableAutomaticMpaSync { get; set; }
    public bool AllowCrossTenantDataSharing { get; set; }
    public bool EnableVesselTracking { get; set; }
    public bool EnableBleachingAlerts { get; set; }
    public bool EnableCitizenScience { get; set; }
}

public class TenantBrandingDto
{
    public string? CustomDomain { get; set; }
    public bool UseCustomDomain { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? ApplicationTitle { get; set; }
    public string? Tagline { get; set; }
}
