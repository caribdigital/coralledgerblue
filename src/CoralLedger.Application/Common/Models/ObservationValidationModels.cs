namespace CoralLedger.Application.Common.Models;

/// <summary>
/// Request model for observation validation.
/// Sprint 4.2 US-4.2.2/4.2.3/4.2.6.
/// </summary>
public record ObservationValidationRequest
{
    /// <summary>
    /// User-claimed observation longitude (WGS84)
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// User-claimed observation latitude (WGS84)
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Date and time of the observation
    /// </summary>
    public required DateTime ObservationTime { get; init; }

    /// <summary>
    /// Type of observation being submitted
    /// </summary>
    public required string ObservationType { get; init; }

    /// <summary>
    /// Optional species identifier for species-specific plausibility checks
    /// </summary>
    public string? SpeciesId { get; init; }

    /// <summary>
    /// Photos with their streams for EXIF extraction
    /// </summary>
    public List<PhotoValidationData> Photos { get; init; } = new();

    /// <summary>
    /// User's trust level for determining validation strictness
    /// </summary>
    public int TrustLevel { get; init; } = 0;

    /// <summary>
    /// Optional notes provided by the observer
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Depth in meters (if applicable for underwater observations)
    /// </summary>
    public double? DepthMeters { get; init; }
}

/// <summary>
/// Photo data for validation including stream for EXIF extraction.
/// </summary>
public record PhotoValidationData
{
    /// <summary>
    /// Original filename of the photo
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Photo stream for EXIF extraction (caller responsible for disposal)
    /// </summary>
    public required Stream PhotoStream { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; init; }
}

/// <summary>
/// Comprehensive validation result for an observation submission.
/// </summary>
public record ObservationValidationResult
{
    /// <summary>
    /// Overall validation passed
    /// </summary>
    public bool IsValid => !HasBlockingIssues;

    /// <summary>
    /// Whether any blocking issues exist
    /// </summary>
    public bool HasBlockingIssues { get; init; }

    /// <summary>
    /// Whether any warnings exist (non-blocking)
    /// </summary>
    public bool HasWarnings { get; init; }

    /// <summary>
    /// Result of geofencing check (Bahamas EEZ)
    /// </summary>
    public required GeofenceValidationResult GeofenceResult { get; init; }

    /// <summary>
    /// Results of EXIF location validation for each photo
    /// </summary>
    public List<ExifLocationValidationResult> ExifResults { get; init; } = new();

    /// <summary>
    /// Plausibility issues found
    /// </summary>
    public List<PlausibilityIssue> PlausibilityIssues { get; init; } = new();

    /// <summary>
    /// Human-readable summary of validation
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Whether observation requires moderator review based on validation
    /// </summary>
    public bool RequiresModerationReview { get; init; }

    /// <summary>
    /// Suggested trust score adjustment (-1 to 1)
    /// </summary>
    public double TrustScoreAdjustment { get; init; }
}

/// <summary>
/// Result of geofence (Bahamas EEZ) validation.
/// US-4.2.3.
/// </summary>
public record GeofenceValidationResult
{
    /// <summary>
    /// Whether the location is within Bahamas EEZ
    /// </summary>
    public bool IsWithinBahamasEez { get; init; }

    /// <summary>
    /// Validated longitude
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Validated latitude
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Whether coordinates are valid WGS84 values
    /// </summary>
    public bool AreCoordinatesValid { get; init; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Distance to nearest EEZ boundary in km (if outside)
    /// </summary>
    public double? DistanceToEezKm { get; init; }
}

/// <summary>
/// Result of EXIF GPS validation against claimed location.
/// US-4.2.2.
/// </summary>
public record ExifLocationValidationResult
{
    /// <summary>
    /// Photo filename this result applies to
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Whether photo contains EXIF GPS data
    /// </summary>
    public bool HasExifGps { get; init; }

    /// <summary>
    /// Whether EXIF location matches claimed location within tolerance
    /// </summary>
    public bool IsLocationValid { get; init; }

    /// <summary>
    /// Distance between claimed and EXIF location in meters
    /// </summary>
    public double? DistanceMeters { get; init; }

    /// <summary>
    /// Tolerance used for validation in meters
    /// </summary>
    public double ToleranceMeters { get; init; }

    /// <summary>
    /// EXIF GPS data if extracted
    /// </summary>
    public ExifGpsData? ExifGps { get; init; }

    /// <summary>
    /// Error message if extraction/validation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// EXIF timestamp if available
    /// </summary>
    public DateTime? ExifTimestamp { get; init; }
}

/// <summary>
/// GPS data extracted from photo EXIF metadata.
/// </summary>
public record ExifGpsData
{
    /// <summary>
    /// Longitude in WGS84
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Latitude in WGS84
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Altitude in meters above sea level (if available)
    /// </summary>
    public double? AltitudeMeters { get; init; }

    /// <summary>
    /// GPS timestamp from EXIF (if available)
    /// </summary>
    public DateTime? GpsTimestamp { get; init; }

    /// <summary>
    /// GPS accuracy/DOP if available
    /// </summary>
    public double? AccuracyMeters { get; init; }
}

/// <summary>
/// Plausibility issue found during validation.
/// US-4.2.6.
/// </summary>
public record PlausibilityIssue
{
    /// <summary>
    /// Type of plausibility check that flagged this issue
    /// </summary>
    public required PlausibilityCheckType CheckType { get; init; }

    /// <summary>
    /// Severity of the issue
    /// </summary>
    public required PlausibilityIssueSeverity Severity { get; init; }

    /// <summary>
    /// Human-readable description of the issue
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Field or aspect that triggered the issue
    /// </summary>
    public string? AffectedField { get; init; }

    /// <summary>
    /// Expected value or range
    /// </summary>
    public string? ExpectedValue { get; init; }

    /// <summary>
    /// Actual value found
    /// </summary>
    public string? ActualValue { get; init; }
}

/// <summary>
/// Types of plausibility checks performed.
/// </summary>
public enum PlausibilityCheckType
{
    /// <summary>
    /// Observation time is in the future
    /// </summary>
    FutureTimestamp,

    /// <summary>
    /// Observation time is suspiciously old
    /// </summary>
    AncientTimestamp,

    /// <summary>
    /// Timestamp doesn't match EXIF timestamp
    /// </summary>
    TimestampMismatch,

    /// <summary>
    /// Depth is implausible for location
    /// </summary>
    ImplausibleDepth,

    /// <summary>
    /// Species not typically found in this area
    /// </summary>
    UnexpectedSpeciesRange,

    /// <summary>
    /// Species observation outside normal season
    /// </summary>
    OutOfSeason,

    /// <summary>
    /// Photo appears to be a screenshot or processed image
    /// </summary>
    ProcessedImage,

    /// <summary>
    /// Location is on land
    /// </summary>
    LocationOnLand,

    /// <summary>
    /// Multiple observations too close together in time
    /// </summary>
    RapidSuccessiveSubmissions,

    /// <summary>
    /// Generic data quality issue
    /// </summary>
    DataQuality
}

/// <summary>
/// Severity levels for plausibility issues.
/// </summary>
public enum PlausibilityIssueSeverity
{
    /// <summary>
    /// Informational only - no action needed
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning - may require review
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error - likely invalid submission, requires review
    /// </summary>
    Error = 2,

    /// <summary>
    /// Blocking - submission rejected automatically
    /// </summary>
    Blocking = 3
}
