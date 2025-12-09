using CoralLedger.Application.Common.Models;

namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Service for validating citizen science observation submissions.
/// Sprint 4.2 US-4.2.2/4.2.3/4.2.6: Comprehensive validation including EXIF, geofencing, and plausibility.
/// Addresses Dr. Thorne Rule 10: Coordinate validation gates.
/// Addresses Dr. Bethel Risk 3.1: GIGO prevention through multi-layer validation.
/// </summary>
public interface IObservationValidationService
{
    /// <summary>
    /// Validates the complete observation submission including location and photos.
    /// </summary>
    /// <param name="request">The observation validation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive validation result with all checks</returns>
    Task<ObservationValidationResult> ValidateObservationAsync(
        ObservationValidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that coordinates fall within Bahamas EEZ boundaries.
    /// US-4.2.3: Geofencing validation.
    /// </summary>
    /// <param name="longitude">Longitude in WGS84</param>
    /// <param name="latitude">Latitude in WGS84</param>
    /// <returns>True if within Bahamas EEZ</returns>
    bool ValidateWithinBahamasEez(double longitude, double latitude);

    /// <summary>
    /// Compares photo EXIF GPS coordinates against claimed observation location.
    /// US-4.2.2: EXIF validation with 500m tolerance (Dr. Thorne's rule).
    /// </summary>
    /// <param name="claimedLongitude">User-claimed longitude</param>
    /// <param name="claimedLatitude">User-claimed latitude</param>
    /// <param name="exifLongitude">EXIF-extracted longitude</param>
    /// <param name="exifLatitude">EXIF-extracted latitude</param>
    /// <param name="toleranceMeters">Maximum allowed distance (default 500m)</param>
    /// <returns>Result with distance and validity</returns>
    ExifLocationValidationResult ValidateExifLocation(
        double claimedLongitude,
        double claimedLatitude,
        double exifLongitude,
        double exifLatitude,
        double toleranceMeters = 500);

    /// <summary>
    /// Performs plausibility checks on observation data.
    /// US-4.2.6: Prevent obviously invalid submissions.
    /// </summary>
    /// <param name="request">The observation request</param>
    /// <returns>List of plausibility issues found</returns>
    List<PlausibilityIssue> CheckPlausibility(ObservationValidationRequest request);

    /// <summary>
    /// Extracts GPS coordinates from photo EXIF metadata.
    /// </summary>
    /// <param name="photoStream">Photo file stream</param>
    /// <returns>GPS coordinates if present, null otherwise</returns>
    Task<ExifGpsData?> ExtractExifGpsAsync(Stream photoStream);
}
