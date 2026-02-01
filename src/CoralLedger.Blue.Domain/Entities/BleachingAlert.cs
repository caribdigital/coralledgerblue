using CoralLedger.Blue.Domain.Common;
using CoralLedger.Blue.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Domain.Entities;

/// <summary>
/// Represents coral bleaching heat stress data from NOAA Coral Reef Watch
/// Sourced from ERDDAP NOAA_DHW dataset (5km resolution)
/// </summary>
public class BleachingAlert : BaseEntity, IAuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Point Location { get; private set; } = null!;
    public DateOnly Date { get; private set; }
    public BleachingAlertLevel AlertLevel { get; private set; }

    // NOAA CRW metrics
    public double SeaSurfaceTemperature { get; private set; }     // Degrees Celsius
    public double SstAnomaly { get; private set; }                // Difference from climatology
    public double? HotSpot { get; private set; }                  // SST above bleaching threshold
    public double DegreeHeatingWeek { get; private set; }         // DHW in degree C-weeks

    // Reference to MPA if within protected area
    public Guid? MarineProtectedAreaId { get; private set; }
    public MarineProtectedArea? MarineProtectedArea { get; private set; }

    // Reference to Reef if tracking specific reef
    public Guid? ReefId { get; private set; }
    public Reef? Reef { get; private set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private BleachingAlert() { }

    public static BleachingAlert Create(
        Guid tenantId,
        Point location,
        DateOnly date,
        double sst,
        double sstAnomaly,
        double dhw,
        double? hotSpot = null,
        Guid? mpaId = null,
        Guid? reefId = null)
    {
        var alert = new BleachingAlert
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Location = location,
            Date = date,
            SeaSurfaceTemperature = sst,
            SstAnomaly = sstAnomaly,
            HotSpot = hotSpot,
            DegreeHeatingWeek = dhw,
            MarineProtectedAreaId = mpaId,
            ReefId = reefId,
            CreatedAt = DateTime.UtcNow
        };

        alert.AlertLevel = CalculateAlertLevel(dhw, hotSpot);
        return alert;
    }

    /// <summary>
    /// Calculate NOAA CRW Bleaching Alert Level based on DHW and HotSpot values
    /// Based on NOAA CRW Alert Area product version 3.1
    /// </summary>
    private static BleachingAlertLevel CalculateAlertLevel(double dhw, double? hotSpot)
    {
        // Alert Level 5: Extreme heat stress (DHW >= 20)
        if (dhw >= 20)
            return BleachingAlertLevel.AlertLevel5;

        // Alert Level 4: Severe heat stress (DHW >= 16)
        if (dhw >= 16)
            return BleachingAlertLevel.AlertLevel4;

        // Alert Level 3: Very high heat stress (DHW >= 12)
        if (dhw >= 12)
            return BleachingAlertLevel.AlertLevel3;

        // Alert Level 2: Significant bleaching expected (DHW >= 8)
        if (dhw >= 8)
            return BleachingAlertLevel.AlertLevel2;

        // Alert Level 1: Bleaching likely (DHW >= 4 with current HotSpot >= 1)
        if (dhw >= 4 && (hotSpot ?? 0) >= 1)
            return BleachingAlertLevel.AlertLevel1;

        // Bleaching Warning: Heat stress building (4 <= DHW < 8, but no active hotspot)
        if (dhw >= 4)
            return BleachingAlertLevel.BleachingWarning;

        // Bleaching Watch: Some heat stress present (0 < DHW < 4)
        if (dhw > 0)
            return BleachingAlertLevel.BleachingWatch;

        return BleachingAlertLevel.NoStress;
    }

    public void UpdateMetrics(double sst, double sstAnomaly, double dhw, double? hotSpot)
    {
        SeaSurfaceTemperature = sst;
        SstAnomaly = sstAnomaly;
        DegreeHeatingWeek = dhw;
        HotSpot = hotSpot;
        AlertLevel = CalculateAlertLevel(dhw, hotSpot);
        ModifiedAt = DateTime.UtcNow;
    }
}
