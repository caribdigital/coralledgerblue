using CoralLedger.Blue.Application.Common.Models;

namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Client for AIS (Automatic Identification System) vessel tracking data
/// </summary>
public interface IAisClient
{
    /// <summary>
    /// Whether the AIS client is configured and enabled
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Get current vessel positions in the configured bounding box
    /// </summary>
    Task<ServiceResult<IReadOnlyList<AisVesselPosition>>> GetVesselPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get vessel positions near a specific location
    /// </summary>
    Task<ServiceResult<IReadOnlyList<AisVesselPosition>>> GetVesselPositionsNearAsync(
        double longitude,
        double latitude,
        double radiusKm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get track history for a specific vessel
    /// </summary>
    Task<ServiceResult<IReadOnlyList<AisVesselPosition>>> GetVesselTrackAsync(
        string mmsi,
        int hours = 24,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AIS vessel position data
/// </summary>
public record AisVesselPosition
{
    public required string Mmsi { get; init; }
    public string? Imo { get; init; }
    public required string Name { get; init; }
    public string? CallSign { get; init; }
    public required double Longitude { get; init; }
    public required double Latitude { get; init; }
    public double? Speed { get; init; }
    public double? Course { get; init; }
    public double? Heading { get; init; }
    public string? Destination { get; init; }
    public string? VesselType { get; init; }
    public string? Flag { get; init; }
    public double? Length { get; init; }
    public double? Width { get; init; }
    public DateTime Timestamp { get; init; }
    public string? NavigationStatus { get; init; }
}
