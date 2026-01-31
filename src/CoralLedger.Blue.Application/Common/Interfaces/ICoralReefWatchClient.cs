using CoralLedger.Blue.Application.Common.Models;

namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Client interface for NOAA Coral Reef Watch ERDDAP data
/// https://coastwatch.pfeg.noaa.gov/erddap/griddap/NOAA_DHW.html
/// </summary>
public interface ICoralReefWatchClient
{
    /// <summary>
    /// Get bleaching heat stress data for a specific location and date
    /// </summary>
    Task<ServiceResult<CrwBleachingData?>> GetBleachingDataAsync(
        double longitude,
        double latitude,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get bleaching heat stress data for a geographic region and date range
    /// </summary>
    Task<ServiceResult<IEnumerable<CrwBleachingData>>> GetBleachingDataForRegionAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current bleaching alerts for the Bahamas region
    /// </summary>
    Task<ServiceResult<IEnumerable<CrwBleachingData>>> GetBahamasBleachingAlertsAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get time series of bleaching data for a specific location
    /// </summary>
    Task<ServiceResult<IEnumerable<CrwBleachingData>>> GetBleachingTimeSeriesAsync(
        double longitude,
        double latitude,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Coral Reef Watch bleaching data from NOAA ERDDAP
/// </summary>
public record CrwBleachingData
{
    public double Longitude { get; init; }
    public double Latitude { get; init; }
    public DateOnly Date { get; init; }

    /// <summary>
    /// Sea Surface Temperature in degrees Celsius (CoralTemp product)
    /// </summary>
    public double SeaSurfaceTemperature { get; init; }

    /// <summary>
    /// SST Anomaly - difference from climatological baseline
    /// </summary>
    public double SstAnomaly { get; init; }

    /// <summary>
    /// HotSpot - positive SST anomaly above bleaching threshold
    /// Null if SST is at or below the bleaching threshold
    /// </summary>
    public double? HotSpot { get; init; }

    /// <summary>
    /// Degree Heating Week - accumulated heat stress over 12 weeks
    /// Units: degree C-weeks
    /// </summary>
    public double DegreeHeatingWeek { get; init; }

    /// <summary>
    /// NOAA CRW Bleaching Alert Area level (0-5)
    /// </summary>
    public int AlertLevel { get; init; }
}
