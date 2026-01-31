using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Common.Models;
using CoralLedger.Blue.Infrastructure.Common;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Service for validating citizen science observation submissions.
/// Sprint 4.2 US-4.2.2/4.2.3/4.2.6: EXIF validation, geofencing, plausibility checks.
/// Addresses Dr. Thorne Rule 10: Coordinate validation gates.
/// Addresses Dr. Bethel Risk 3.1: GIGO prevention through multi-layer validation.
/// </summary>
public class ObservationValidationService : IObservationValidationService
{
    private readonly ILogger<ObservationValidationService> _logger;
    private readonly ICoordinateTransformation _wgs84ToUtm;
    private readonly GeometryFactory _geometryFactory;

    // Validation thresholds
    private const double DefaultExifToleranceMeters = 500; // Dr. Thorne's rule
    private const int MaxObservationAgeHours = 168; // 7 days
    private const int MaxPhotoAgeHours = 336; // 14 days
    private const double MaxReasonableDepthMeters = 150; // Maximum dive depth
    private const double MinWaterDepthMeters = 0.5; // Minimum reasonable water depth

    public ObservationValidationService(ILogger<ObservationValidationService> logger)
    {
        _logger = logger;
        _geometryFactory = BahamasSpatialConstants.GeometryFactory;

        // Set up coordinate transformation for distance calculations (Thorne Rule 1)
        var wgs84 = GeographicCoordinateSystem.WGS84;
        var utm18n = ProjectedCoordinateSystem.WGS84_UTM(18, true);
        var factory = new CoordinateTransformationFactory();
        _wgs84ToUtm = factory.CreateFromCoordinateSystems(wgs84, utm18n);
    }

    /// <inheritdoc/>
    public async Task<ObservationValidationResult> ValidateObservationAsync(
        ObservationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Validating observation at ({Lon}, {Lat}) with {PhotoCount} photos",
            request.Longitude, request.Latitude, request.Photos.Count);

        var geofenceResult = ValidateGeofence(request.Longitude, request.Latitude);
        var exifResults = new List<ExifLocationValidationResult>();
        var plausibilityIssues = CheckPlausibility(request);

        // Validate each photo's EXIF data
        foreach (var photo in request.Photos)
        {
            var exifResult = await ValidatePhotoExifAsync(
                photo,
                request.Longitude,
                request.Latitude,
                request.TrustLevel,
                cancellationToken).ConfigureAwait(false);
            exifResults.Add(exifResult);
        }

        // Determine if blocking issues exist
        var hasBlockingIssues = !geofenceResult.IsWithinBahamasEez ||
                               !geofenceResult.AreCoordinatesValid ||
                               plausibilityIssues.Any(p => p.Severity == PlausibilityIssueSeverity.Blocking);

        // Determine if warnings exist
        var hasWarnings = plausibilityIssues.Any(p => p.Severity >= PlausibilityIssueSeverity.Warning) ||
                         exifResults.Any(e => e.HasExifGps && !e.IsLocationValid);

        // Calculate trust score adjustment
        var trustAdjustment = CalculateTrustAdjustment(geofenceResult, exifResults, plausibilityIssues);

        // Determine if moderation is required
        var requiresModeration = request.TrustLevel < 1 || // Unverified users always need review
                                hasWarnings ||
                                exifResults.All(e => !e.HasExifGps); // No EXIF GPS in any photo

        var summary = BuildSummary(geofenceResult, exifResults, plausibilityIssues, hasBlockingIssues);

        _logger.LogInformation(
            "Observation validation complete: Valid={IsValid}, Blocking={HasBlocking}, Warnings={HasWarnings}",
            !hasBlockingIssues, hasBlockingIssues, hasWarnings);

        return new ObservationValidationResult
        {
            HasBlockingIssues = hasBlockingIssues,
            HasWarnings = hasWarnings,
            GeofenceResult = geofenceResult,
            ExifResults = exifResults,
            PlausibilityIssues = plausibilityIssues,
            Summary = summary,
            RequiresModerationReview = requiresModeration,
            TrustScoreAdjustment = trustAdjustment
        };
    }

    /// <inheritdoc/>
    public bool ValidateWithinBahamasEez(double longitude, double latitude)
    {
        return BahamasSpatialConstants.IsWithinBahamasBounds(longitude, latitude);
    }

    /// <inheritdoc/>
    public ExifLocationValidationResult ValidateExifLocation(
        double claimedLongitude,
        double claimedLatitude,
        double exifLongitude,
        double exifLatitude,
        double toleranceMeters = DefaultExifToleranceMeters)
    {
        try
        {
            var distance = CalculateDistanceMeters(claimedLongitude, claimedLatitude, exifLongitude, exifLatitude);
            var isValid = distance <= toleranceMeters;

            return new ExifLocationValidationResult
            {
                FileName = "unknown",
                HasExifGps = true,
                IsLocationValid = isValid,
                DistanceMeters = distance,
                ToleranceMeters = toleranceMeters,
                ExifGps = new ExifGpsData
                {
                    Longitude = exifLongitude,
                    Latitude = exifLatitude
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating EXIF location");
            return new ExifLocationValidationResult
            {
                FileName = "unknown",
                HasExifGps = true,
                IsLocationValid = false,
                ToleranceMeters = toleranceMeters,
                ErrorMessage = $"Error calculating distance: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public List<PlausibilityIssue> CheckPlausibility(ObservationValidationRequest request)
    {
        var issues = new List<PlausibilityIssue>();

        // Check: Future timestamp
        if (request.ObservationTime > DateTime.UtcNow.AddMinutes(15))
        {
            issues.Add(new PlausibilityIssue
            {
                CheckType = PlausibilityCheckType.FutureTimestamp,
                Severity = PlausibilityIssueSeverity.Blocking,
                Description = "Observation time is in the future",
                AffectedField = "ObservationTime",
                ExpectedValue = $"Before {DateTime.UtcNow:u}",
                ActualValue = request.ObservationTime.ToString("u")
            });
        }

        // Check: Ancient timestamp
        var maxAge = TimeSpan.FromHours(MaxObservationAgeHours);
        if (request.ObservationTime < DateTime.UtcNow - maxAge)
        {
            issues.Add(new PlausibilityIssue
            {
                CheckType = PlausibilityCheckType.AncientTimestamp,
                Severity = PlausibilityIssueSeverity.Warning,
                Description = $"Observation is older than {MaxObservationAgeHours / 24} days",
                AffectedField = "ObservationTime",
                ExpectedValue = $"Within last {MaxObservationAgeHours / 24} days",
                ActualValue = request.ObservationTime.ToString("u")
            });
        }

        // Check: Implausible depth
        if (request.DepthMeters.HasValue)
        {
            if (request.DepthMeters < MinWaterDepthMeters)
            {
                issues.Add(new PlausibilityIssue
                {
                    CheckType = PlausibilityCheckType.ImplausibleDepth,
                    Severity = PlausibilityIssueSeverity.Warning,
                    Description = "Depth is too shallow for underwater observation",
                    AffectedField = "DepthMeters",
                    ExpectedValue = $">= {MinWaterDepthMeters}m",
                    ActualValue = $"{request.DepthMeters}m"
                });
            }
            else if (request.DepthMeters > MaxReasonableDepthMeters)
            {
                issues.Add(new PlausibilityIssue
                {
                    CheckType = PlausibilityCheckType.ImplausibleDepth,
                    Severity = PlausibilityIssueSeverity.Error,
                    Description = "Depth exceeds maximum recreational dive depth",
                    AffectedField = "DepthMeters",
                    ExpectedValue = $"<= {MaxReasonableDepthMeters}m",
                    ActualValue = $"{request.DepthMeters}m"
                });
            }
        }

        // Check: No photos provided
        if (request.Photos.Count == 0)
        {
            issues.Add(new PlausibilityIssue
            {
                CheckType = PlausibilityCheckType.DataQuality,
                Severity = PlausibilityIssueSeverity.Warning,
                Description = "No photos provided with observation",
                AffectedField = "Photos"
            });
        }

        // Check: Observation type is valid
        if (string.IsNullOrWhiteSpace(request.ObservationType))
        {
            issues.Add(new PlausibilityIssue
            {
                CheckType = PlausibilityCheckType.DataQuality,
                Severity = PlausibilityIssueSeverity.Error,
                Description = "Observation type is required",
                AffectedField = "ObservationType"
            });
        }

        return issues;
    }

    /// <inheritdoc/>
    public async Task<ExifGpsData?> ExtractExifGpsAsync(Stream photoStream)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(photoStream);
            var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();

            if (gpsDirectory == null)
            {
                _logger.LogDebug("No GPS directory found in image EXIF data");
                return null;
            }

            var geoLocation = gpsDirectory.GetGeoLocation();
            if (geoLocation == null)
            {
                _logger.LogDebug("GPS directory present but no valid location data");
                return null;
            }

            var result = new ExifGpsData
            {
                Longitude = geoLocation.Longitude,
                Latitude = geoLocation.Latitude
            };

            // Try to extract altitude
            if (gpsDirectory.TryGetRational(GpsDirectory.TagAltitude, out var altitude))
            {
                var altitudeRef = gpsDirectory.GetInt32(GpsDirectory.TagAltitudeRef);
                var altitudeValue = altitude.ToDouble();
                // AltitudeRef: 0 = above sea level, 1 = below sea level
                if (altitudeRef == 1)
                    altitudeValue = -altitudeValue;
                result = result with { AltitudeMeters = altitudeValue };
            }

            // Try to extract timestamp from GPS date/time
            if (gpsDirectory.TryGetDateTime(GpsDirectory.TagDateStamp, out var gpsDate))
            {
                result = result with { GpsTimestamp = gpsDate };
            }

            // Try to extract DOP (accuracy)
            if (gpsDirectory.TryGetRational(GpsDirectory.TagDop, out var dop))
            {
                // DOP is a multiplier, not meters. Rough conversion: accuracy ~= DOP * 5m
                result = result with { AccuracyMeters = dop.ToDouble() * 5 };
            }

            _logger.LogDebug(
                "Extracted EXIF GPS: ({Lon}, {Lat}), Alt={Alt}m, Time={Time}",
                result.Longitude, result.Latitude, result.AltitudeMeters, result.GpsTimestamp);

            return result;
        }
        catch (ImageProcessingException ex)
        {
            _logger.LogWarning(ex, "Error processing image metadata");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error extracting EXIF GPS data");
            return null;
        }
    }

    private GeofenceValidationResult ValidateGeofence(double longitude, double latitude)
    {
        // First, validate WGS84 coordinate ranges
        var validLon = BahamasSpatialConstants.IsValidLongitude(longitude);
        var validLat = BahamasSpatialConstants.IsValidLatitude(latitude);

        if (!validLon || !validLat)
        {
            return new GeofenceValidationResult
            {
                IsWithinBahamasEez = false,
                Longitude = longitude,
                Latitude = latitude,
                AreCoordinatesValid = false,
                ErrorMessage = $"Invalid coordinates: Longitude must be -180 to 180, Latitude must be -90 to 90. " +
                              $"Got: ({longitude}, {latitude})"
            };
        }

        // Check if within Bahamas EEZ
        var withinEez = BahamasSpatialConstants.IsWithinBahamasBounds(longitude, latitude);

        double? distanceToEez = null;
        if (!withinEez)
        {
            distanceToEez = CalculateDistanceToEezKm(longitude, latitude);
        }

        return new GeofenceValidationResult
        {
            IsWithinBahamasEez = withinEez,
            Longitude = longitude,
            Latitude = latitude,
            AreCoordinatesValid = true,
            ErrorMessage = withinEez ? null : "Location is outside Bahamas Exclusive Economic Zone",
            DistanceToEezKm = distanceToEez
        };
    }

    private async Task<ExifLocationValidationResult> ValidatePhotoExifAsync(
        PhotoValidationData photo,
        double claimedLongitude,
        double claimedLatitude,
        int trustLevel,
        CancellationToken cancellationToken)
    {
        try
        {
            // Reset stream position for reading
            if (photo.PhotoStream.CanSeek)
            {
                photo.PhotoStream.Position = 0;
            }

            var exifGps = await ExtractExifGpsAsync(photo.PhotoStream).ConfigureAwait(false);

            if (exifGps == null)
            {
                _logger.LogDebug("No EXIF GPS data found in photo: {FileName}", photo.FileName);
                return new ExifLocationValidationResult
                {
                    FileName = photo.FileName,
                    HasExifGps = false,
                    IsLocationValid = trustLevel >= 2, // Expert and above trusted without EXIF
                    ToleranceMeters = DefaultExifToleranceMeters,
                    ErrorMessage = "Photo does not contain GPS metadata"
                };
            }

            var distance = CalculateDistanceMeters(
                claimedLongitude, claimedLatitude,
                exifGps.Longitude, exifGps.Latitude);

            // Adjust tolerance based on trust level
            var tolerance = trustLevel switch
            {
                >= 3 => DefaultExifToleranceMeters * 2, // Professionals get 1km tolerance
                >= 2 => DefaultExifToleranceMeters * 1.5, // Experts get 750m
                _ => DefaultExifToleranceMeters // Default 500m
            };

            var isValid = distance <= tolerance;

            _logger.LogDebug(
                "EXIF validation for {FileName}: Distance={Distance:F1}m, Tolerance={Tolerance}m, Valid={Valid}",
                photo.FileName, distance, tolerance, isValid);

            return new ExifLocationValidationResult
            {
                FileName = photo.FileName,
                HasExifGps = true,
                IsLocationValid = isValid,
                DistanceMeters = distance,
                ToleranceMeters = tolerance,
                ExifGps = exifGps,
                ExifTimestamp = exifGps.GpsTimestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing photo EXIF: {FileName}", photo.FileName);
            return new ExifLocationValidationResult
            {
                FileName = photo.FileName,
                HasExifGps = false,
                IsLocationValid = false,
                ToleranceMeters = DefaultExifToleranceMeters,
                ErrorMessage = $"Error reading photo metadata: {ex.Message}"
            };
        }
    }

    private double CalculateDistanceMeters(double lon1, double lat1, double lon2, double lat2)
    {
        // Transform to UTM for accurate distance calculation (Thorne Rule 1)
        var coord1 = new[] { lon1, lat1 };
        var coord2 = new[] { lon2, lat2 };

        var utm1 = _wgs84ToUtm.MathTransform.Transform(coord1);
        var utm2 = _wgs84ToUtm.MathTransform.Transform(coord2);

        var dx = utm2[0] - utm1[0];
        var dy = utm2[1] - utm1[1];

        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double CalculateDistanceToEezKm(double longitude, double latitude)
    {
        // Calculate distance to nearest EEZ boundary edge
        var clampedLon = Math.Max(BahamasSpatialConstants.MinLongitude,
                          Math.Min(BahamasSpatialConstants.MaxLongitude, longitude));
        var clampedLat = Math.Max(BahamasSpatialConstants.MinLatitude,
                          Math.Min(BahamasSpatialConstants.MaxLatitude, latitude));

        var distanceMeters = CalculateDistanceMeters(longitude, latitude, clampedLon, clampedLat);
        return distanceMeters / 1000.0;
    }

    private double CalculateTrustAdjustment(
        GeofenceValidationResult geofenceResult,
        List<ExifLocationValidationResult> exifResults,
        List<PlausibilityIssue> plausibilityIssues)
    {
        double adjustment = 0;

        // Positive adjustments
        if (geofenceResult.IsWithinBahamasEez)
            adjustment += 0.1;

        var validExifCount = exifResults.Count(e => e.HasExifGps && e.IsLocationValid);
        adjustment += validExifCount * 0.1;

        // Negative adjustments
        if (!geofenceResult.AreCoordinatesValid)
            adjustment -= 0.5;

        var invalidExifCount = exifResults.Count(e => e.HasExifGps && !e.IsLocationValid);
        adjustment -= invalidExifCount * 0.2;

        var errorCount = plausibilityIssues.Count(p => p.Severity >= PlausibilityIssueSeverity.Error);
        adjustment -= errorCount * 0.15;

        // Clamp to -1 to 1 range
        return Math.Max(-1, Math.Min(1, adjustment));
    }

    private string BuildSummary(
        GeofenceValidationResult geofenceResult,
        List<ExifLocationValidationResult> exifResults,
        List<PlausibilityIssue> plausibilityIssues,
        bool hasBlockingIssues)
    {
        if (hasBlockingIssues)
        {
            if (!geofenceResult.AreCoordinatesValid)
                return "Invalid coordinates provided";
            if (!geofenceResult.IsWithinBahamasEez)
                return $"Location outside Bahamas EEZ ({geofenceResult.DistanceToEezKm:F1}km from boundary)";
            if (plausibilityIssues.Any(p => p.Severity == PlausibilityIssueSeverity.Blocking))
                return plausibilityIssues.First(p => p.Severity == PlausibilityIssueSeverity.Blocking).Description;
        }

        var validPhotos = exifResults.Count(e => e.HasExifGps && e.IsLocationValid);
        var totalWithGps = exifResults.Count(e => e.HasExifGps);

        if (totalWithGps == 0 && exifResults.Count > 0)
            return "No photos contain GPS metadata - manual verification required";

        if (validPhotos < totalWithGps)
            return $"Location mismatch in {totalWithGps - validPhotos} of {totalWithGps} geotagged photos";

        if (plausibilityIssues.Any(p => p.Severity >= PlausibilityIssueSeverity.Warning))
            return $"Validation passed with {plausibilityIssues.Count} warning(s)";

        return "All validation checks passed";
    }
}
