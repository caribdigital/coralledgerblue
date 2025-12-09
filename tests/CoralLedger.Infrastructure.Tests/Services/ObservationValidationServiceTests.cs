using CoralLedger.Application.Common.Models;
using CoralLedger.Infrastructure.Common;
using CoralLedger.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoralLedger.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ObservationValidationService - verifies EXIF validation, geofencing,
/// and plausibility checks for Sprint 4.2 citizen science features.
/// </summary>
public class ObservationValidationServiceTests
{
    private readonly Mock<ILogger<ObservationValidationService>> _loggerMock;
    private readonly ObservationValidationService _service;

    // Test coordinates within Bahamas EEZ
    private const double NassauLon = -77.3554;
    private const double NassauLat = 25.0480;
    private const double ExumaLon = -76.5833;
    private const double ExumaLat = 24.4667;

    // Test coordinates outside Bahamas EEZ
    private const double KeyWestLon = -81.78;
    private const double KeyWestLat = 24.5551;
    private const double MiamiLon = -80.1918;
    private const double MiamiLat = 25.7617;

    public ObservationValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ObservationValidationService>>();
        _service = new ObservationValidationService(_loggerMock.Object);
    }

    // ============ Geofence Validation Tests ============

    [Fact]
    public void ValidateWithinBahamasEez_NassauCoordinates_ReturnsTrue()
    {
        // Act
        var result = _service.ValidateWithinBahamasEez(NassauLon, NassauLat);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateWithinBahamasEez_ExumaCoordinates_ReturnsTrue()
    {
        // Act
        var result = _service.ValidateWithinBahamasEez(ExumaLon, ExumaLat);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateWithinBahamasEez_KeyWestCoordinates_ReturnsFalse()
    {
        // Act - Key West is west of the Bahamas EEZ boundary (-80.5)
        var result = _service.ValidateWithinBahamasEez(KeyWestLon, KeyWestLat);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateWithinBahamasEez_BoundaryCoordinates_ReturnsTrue()
    {
        // Test on boundary edges
        var minLon = BahamasSpatialConstants.MinLongitude;
        var maxLon = BahamasSpatialConstants.MaxLongitude;
        var minLat = BahamasSpatialConstants.MinLatitude;
        var maxLat = BahamasSpatialConstants.MaxLatitude;

        // All boundary points should be within
        _service.ValidateWithinBahamasEez(minLon, minLat).Should().BeTrue();
        _service.ValidateWithinBahamasEez(maxLon, maxLat).Should().BeTrue();
        _service.ValidateWithinBahamasEez(minLon, maxLat).Should().BeTrue();
        _service.ValidateWithinBahamasEez(maxLon, minLat).Should().BeTrue();
    }

    // ============ EXIF Location Validation Tests ============

    [Fact]
    public void ValidateExifLocation_ExactMatch_ReturnsValid()
    {
        // Act - Same coordinates should validate
        var result = _service.ValidateExifLocation(
            NassauLon, NassauLat,
            NassauLon, NassauLat);

        // Assert
        result.IsLocationValid.Should().BeTrue();
        result.HasExifGps.Should().BeTrue();
        result.DistanceMeters.Should().BeLessThan(1);
    }

    [Fact]
    public void ValidateExifLocation_Within500m_ReturnsValid()
    {
        // Arrange - Approx 300m offset
        var exifLon = NassauLon + 0.003;
        var exifLat = NassauLat + 0.002;

        // Act
        var result = _service.ValidateExifLocation(
            NassauLon, NassauLat,
            exifLon, exifLat);

        // Assert
        result.IsLocationValid.Should().BeTrue();
        result.DistanceMeters.Should().BeLessThan(500);
    }

    [Fact]
    public void ValidateExifLocation_Beyond500m_ReturnsInvalid()
    {
        // Arrange - Approx 1km offset
        var exifLon = NassauLon + 0.01;
        var exifLat = NassauLat + 0.005;

        // Act
        var result = _service.ValidateExifLocation(
            NassauLon, NassauLat,
            exifLon, exifLat,
            500);

        // Assert
        result.IsLocationValid.Should().BeFalse();
        result.DistanceMeters.Should().BeGreaterThan(500);
    }

    [Fact]
    public void ValidateExifLocation_CustomTolerance_RespectsThreshold()
    {
        // Arrange - ~800m offset
        var exifLon = NassauLon + 0.008;
        var exifLat = NassauLat;

        // Act - With 1km tolerance
        var result = _service.ValidateExifLocation(
            NassauLon, NassauLat,
            exifLon, exifLat,
            1000);

        // Assert
        result.IsLocationValid.Should().BeTrue();
        result.ToleranceMeters.Should().Be(1000);
    }

    // ============ Plausibility Check Tests ============

    [Fact]
    public void CheckPlausibility_FutureTimestamp_ReturnsBlockingIssue()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ObservationTime = DateTime.UtcNow.AddDays(1) };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().Contain(i =>
            i.CheckType == PlausibilityCheckType.FutureTimestamp &&
            i.Severity == PlausibilityIssueSeverity.Blocking);
    }

    [Fact]
    public void CheckPlausibility_AncientTimestamp_ReturnsWarning()
    {
        // Arrange - 2 weeks old
        var request = CreateValidRequest();
        request = request with { ObservationTime = DateTime.UtcNow.AddDays(-14) };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().Contain(i =>
            i.CheckType == PlausibilityCheckType.AncientTimestamp &&
            i.Severity == PlausibilityIssueSeverity.Warning);
    }

    [Fact]
    public void CheckPlausibility_ValidTimestamp_NoTimestampIssues()
    {
        // Arrange - 1 day old
        var request = CreateValidRequest();
        request = request with { ObservationTime = DateTime.UtcNow.AddDays(-1) };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().NotContain(i =>
            i.CheckType == PlausibilityCheckType.FutureTimestamp ||
            i.CheckType == PlausibilityCheckType.AncientTimestamp);
    }

    [Fact]
    public void CheckPlausibility_ImplausibleShallowDepth_ReturnsWarning()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { DepthMeters = 0.1 }; // Too shallow

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().Contain(i =>
            i.CheckType == PlausibilityCheckType.ImplausibleDepth &&
            i.Severity == PlausibilityIssueSeverity.Warning);
    }

    [Fact]
    public void CheckPlausibility_ImplausibleDeepDepth_ReturnsError()
    {
        // Arrange - Beyond recreational dive limit
        var request = CreateValidRequest();
        request = request with { DepthMeters = 200 };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().Contain(i =>
            i.CheckType == PlausibilityCheckType.ImplausibleDepth &&
            i.Severity == PlausibilityIssueSeverity.Error);
    }

    [Fact]
    public void CheckPlausibility_ValidDepth_NoDepthIssues()
    {
        // Arrange - Normal dive depth
        var request = CreateValidRequest();
        request = request with { DepthMeters = 15 };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().NotContain(i => i.CheckType == PlausibilityCheckType.ImplausibleDepth);
    }

    [Fact]
    public void CheckPlausibility_NoPhotos_ReturnsWarning()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { Photos = new List<PhotoValidationData>() };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().Contain(i =>
            i.CheckType == PlausibilityCheckType.DataQuality &&
            i.AffectedField == "Photos");
    }

    [Fact]
    public void CheckPlausibility_MissingObservationType_ReturnsError()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ObservationType = "" };

        // Act
        var issues = _service.CheckPlausibility(request);

        // Assert
        issues.Should().Contain(i =>
            i.CheckType == PlausibilityCheckType.DataQuality &&
            i.AffectedField == "ObservationType" &&
            i.Severity == PlausibilityIssueSeverity.Error);
    }

    // ============ Full Validation Tests ============

    [Fact]
    public async Task ValidateObservationAsync_ValidRequest_ReturnsValid()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.HasBlockingIssues.Should().BeFalse();
        result.GeofenceResult.IsWithinBahamasEez.Should().BeTrue();
        result.GeofenceResult.AreCoordinatesValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateObservationAsync_OutsideEez_ReturnsBlocking()
    {
        // Arrange
        var request = CreateValidRequest() with
        {
            Longitude = KeyWestLon,
            Latitude = KeyWestLat
        };

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasBlockingIssues.Should().BeTrue();
        result.GeofenceResult.IsWithinBahamasEez.Should().BeFalse();
        result.GeofenceResult.DistanceToEezKm.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateObservationAsync_InvalidCoordinates_ReturnsBlocking()
    {
        // Arrange - Invalid longitude
        var request = CreateValidRequest() with
        {
            Longitude = 200, // Invalid
            Latitude = NassauLat
        };

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasBlockingIssues.Should().BeTrue();
        result.GeofenceResult.AreCoordinatesValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateObservationAsync_UnverifiedUser_RequiresModeration()
    {
        // Arrange
        var request = CreateValidRequest() with { TrustLevel = 0 };

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        result.RequiresModerationReview.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateObservationAsync_ExpertUser_MayNotRequireModeration()
    {
        // Arrange
        var request = CreateValidRequest() with { TrustLevel = 2 };

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        // Expert users without warnings may bypass moderation
        if (!result.HasWarnings)
        {
            result.RequiresModerationReview.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ValidateObservationAsync_WithWarnings_HasWarningsTrue()
    {
        // Arrange - Ancient observation
        var request = CreateValidRequest() with
        {
            ObservationTime = DateTime.UtcNow.AddDays(-10)
        };

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        result.HasWarnings.Should().BeTrue();
        result.PlausibilityIssues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateObservationAsync_TrustScoreAdjustment_ReflectsValidation()
    {
        // Arrange - Valid request
        var validRequest = CreateValidRequest();

        // Act
        var result = await _service.ValidateObservationAsync(validRequest);

        // Assert - Good observation should get positive adjustment
        result.TrustScoreAdjustment.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ValidateObservationAsync_Summary_ContainsMeaningfulText()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _service.ValidateObservationAsync(request);

        // Assert
        result.Summary.Should().NotBeNullOrEmpty();
        result.Summary.Length.Should().BeGreaterThan(10);
    }

    // ============ Helper Methods ============

    private static ObservationValidationRequest CreateValidRequest()
    {
        return new ObservationValidationRequest
        {
            Longitude = NassauLon,
            Latitude = NassauLat,
            ObservationTime = DateTime.UtcNow.AddHours(-2),
            ObservationType = "CoralBleaching",
            TrustLevel = 1,
            DepthMeters = 10,
            Notes = "Test observation",
            Photos = new List<PhotoValidationData>()
        };
    }
}
