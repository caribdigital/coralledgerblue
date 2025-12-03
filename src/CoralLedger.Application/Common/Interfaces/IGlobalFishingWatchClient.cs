using CoralLedger.Domain.Enums;

namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Client interface for Global Fishing Watch API v3
/// https://globalfishingwatch.org/our-apis/documentation
/// </summary>
public interface IGlobalFishingWatchClient
{
    /// <summary>
    /// Gets whether the client is properly configured with an API token
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Search for vessels by various criteria
    /// </summary>
    Task<IEnumerable<GfwVesselInfo>> SearchVesselsAsync(
        string? query = null,
        string? flag = null,
        VesselType? vesselType = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get vessel details by GFW vessel ID
    /// </summary>
    Task<GfwVesselInfo?> GetVesselByIdAsync(
        string vesselId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get fishing events in a geographic region within a time range
    /// </summary>
    Task<IEnumerable<GfwEvent>> GetFishingEventsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get port visit events for vessels
    /// </summary>
    Task<IEnumerable<GfwEvent>> GetPortVisitsAsync(
        string? vesselId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get vessel encounters (meetings between vessels at sea)
    /// </summary>
    Task<IEnumerable<GfwEvent>> GetEncountersAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get fishing effort statistics for a geographic region
    /// </summary>
    Task<GfwFishingEffortStats> GetFishingEffortStatsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Vessel information from Global Fishing Watch API
/// </summary>
public record GfwVesselInfo
{
    public string VesselId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Mmsi { get; init; }
    public string? Imo { get; init; }
    public string? CallSign { get; init; }
    public string? Flag { get; init; }
    public string? VesselType { get; init; }
    public string? GearType { get; init; }
    public double? LengthMeters { get; init; }
    public double? TonnageGt { get; init; }
    public int? YearBuilt { get; init; }
    public DateTime? LastPositionTime { get; init; }
}

/// <summary>
/// Event information from Global Fishing Watch API
/// </summary>
public record GfwEvent
{
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string VesselId { get; init; } = string.Empty;
    public string? VesselName { get; init; }
    public double Longitude { get; init; }
    public double Latitude { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public double? DurationHours { get; init; }
    public double? DistanceKm { get; init; }
    public string? PortName { get; init; }
    public string? EncounterVesselId { get; init; }
}

/// <summary>
/// Fishing effort statistics from Global Fishing Watch API
/// </summary>
public record GfwFishingEffortStats
{
    public double TotalFishingHours { get; init; }
    public int VesselCount { get; init; }
    public int EventCount { get; init; }
    public Dictionary<string, double> FishingHoursByFlag { get; init; } = new();
    public Dictionary<string, double> FishingHoursByGearType { get; init; } = new();
}
