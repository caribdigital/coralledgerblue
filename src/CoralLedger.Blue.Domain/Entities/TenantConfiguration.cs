using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Configuration settings specific to a tenant (MPA sources, data sharing policies, etc.)
/// </summary>
public class TenantConfiguration : BaseEntity, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    
    // MPA Data Sources
    public string? WdpaApiToken { get; private set; }
    public string? CustomMpaSourceUrl { get; private set; }
    public bool EnableAutomaticMpaSync { get; private set; } = true;
    
    // Data Sharing
    public bool AllowCrossTenantDataSharing { get; private set; } = false;
    public string? SharedDataTenantIds { get; private set; } // Comma-separated list of tenant IDs
    
    // Feature Flags
    public bool EnableVesselTracking { get; private set; } = true;
    public bool EnableBleachingAlerts { get; private set; } = true;
    public bool EnableCitizenScience { get; private set; } = false;
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; private set; } = null!;
    
    private TenantConfiguration() { }
    
    public static TenantConfiguration Create(Guid tenantId)
    {
        var config = new TenantConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        
        return config;
    }
    
    public void UpdateMpaSourceSettings(
        string? wdpaApiToken = null,
        string? customMpaSourceUrl = null,
        bool? enableAutomaticSync = null)
    {
        if (wdpaApiToken != null) WdpaApiToken = wdpaApiToken;
        if (customMpaSourceUrl != null) CustomMpaSourceUrl = customMpaSourceUrl;
        if (enableAutomaticSync.HasValue) EnableAutomaticMpaSync = enableAutomaticSync.Value;
        
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void UpdateDataSharingSettings(
        bool allowCrossTenantSharing,
        string? sharedDataTenantIds = null)
    {
        AllowCrossTenantDataSharing = allowCrossTenantSharing;
        SharedDataTenantIds = sharedDataTenantIds;
        ModifiedAt = DateTime.UtcNow;
    }
    
    public void UpdateFeatureFlags(
        bool? vesselTracking = null,
        bool? bleachingAlerts = null,
        bool? citizenScience = null)
    {
        if (vesselTracking.HasValue) EnableVesselTracking = vesselTracking.Value;
        if (bleachingAlerts.HasValue) EnableBleachingAlerts = bleachingAlerts.Value;
        if (citizenScience.HasValue) EnableCitizenScience = citizenScience.Value;
        
        ModifiedAt = DateTime.UtcNow;
    }
}
