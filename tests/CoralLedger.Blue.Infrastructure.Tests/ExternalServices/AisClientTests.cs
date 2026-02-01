using CoralLedger.Blue.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.ExternalServices;

public class AisClientTests
{
    private readonly Mock<ILogger<AisClient>> _loggerMock;
    private readonly Mock<IOptions<AisOptions>> _optionsMock;

    public AisClientTests()
    {
        _loggerMock = new Mock<ILogger<AisClient>>();
        _optionsMock = new Mock<IOptions<AisOptions>>();
    }

    private AisClient CreateClient(AisOptions? options = null)
    {
        var aisOptions = options ?? new AisOptions
        {
            Enabled = false, // Not configured (demo mode)
            ApiKey = "",
            Provider = "MarineTraffic"
        };

        _optionsMock.Setup(o => o.Value).Returns(aisOptions);

        var httpClient = new HttpClient();
        return new AisClient(httpClient, _optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void IsConfigured_WhenNotEnabled_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        client.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenEnabledButNoApiKey_ReturnsFalse()
    {
        // Arrange
        var options = new AisOptions
        {
            Enabled = true,
            ApiKey = "",
            Provider = "MarineTraffic"
        };
        var client = CreateClient(options);

        // Act & Assert
        client.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenEnabledWithApiKey_ReturnsTrue()
    {
        // Arrange
        var options = new AisOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            Provider = "MarineTraffic",
            BaseUrl = "https://api.marinetraffic.com/"
        };
        var client = CreateClient(options);

        // Act & Assert
        client.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task GetVesselPositionsAsync_WhenNotConfigured_ReturnsDemoData()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var result = await client.GetVesselPositionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().HaveCountGreaterThan(0);
        
        // Verify demo vessels are returned
        var vessels = result.Value!.ToList();
        vessels.Should().Contain(v => v.Mmsi == "311000001"); // BAHAMAS EXPLORER
        vessels.Should().Contain(v => v.Name == "NASSAU PEARL");
    }

    [Fact]
    public async Task GetVesselTrackAsync_WhenNotConfigured_ReturnsDemoTrack()
    {
        // Arrange
        var client = CreateClient();
        var mmsi = "311000001"; // Demo vessel MMSI
        var hours = 24;

        // Act
        var result = await client.GetVesselTrackAsync(mmsi, hours);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        
        var track = result.Value!.ToList();
        
        // Verify track contains between 30-40 points as specified
        track.Should().HaveCountGreaterThanOrEqualTo(30);
        track.Should().HaveCountLessThanOrEqualTo(40);
    }

    [Fact]
    public async Task GetVesselTrackAsync_WhenNotConfigured_ReturnsRealisticTrack()
    {
        // Arrange
        var client = CreateClient();
        var mmsi = "311000001";
        var hours = 24;

        // Act
        var result = await client.GetVesselTrackAsync(mmsi, hours);

        // Assert
        result.Success.Should().BeTrue();
        var track = result.Value!.ToList();
        
        // Verify all track points have the same MMSI
        track.Should().OnlyContain(p => p.Mmsi == mmsi);
        
        // Verify track points are in chronological order (oldest to newest)
        for (int i = 1; i < track.Count; i++)
        {
            track[i].Timestamp.Should().BeOnOrAfter(track[i - 1].Timestamp);
        }
        
        // Verify timestamps span approximately the requested time period
        var firstPoint = track.First();
        var lastPoint = track.Last();
        var timeDifference = lastPoint.Timestamp - firstPoint.Timestamp;
        timeDifference.TotalHours.Should().BeApproximately(hours, 1.0); // Within 1 hour of requested period
        
        // Verify all points have realistic speed values (0-30 knots for most vessels)
        track.Should().OnlyContain(p => p.Speed >= 0 && p.Speed <= 30);
        
        // Verify all points have valid course values (0-360 degrees)
        track.Should().OnlyContain(p => p.Course >= 0 && p.Course <= 360);
        
        // Verify all points have valid coordinates
        track.Should().OnlyContain(p => 
            p.Latitude >= -90 && p.Latitude <= 90 && 
            p.Longitude >= -180 && p.Longitude <= 180);
    }

    [Fact]
    public async Task GetVesselTrackAsync_WhenNotConfigured_ReturnsPathNotRandomPoints()
    {
        // Arrange
        var client = CreateClient();
        var mmsi = "311000001"; // Demo vessel MMSI
        var hours = 12;

        // Act
        var result = await client.GetVesselTrackAsync(mmsi, hours);

        // Assert
        result.Success.Should().BeTrue();
        var track = result.Value!.ToList();
        
        // Verify track points form a logical path by checking consecutive points
        // are reasonably close to each other (not scattered randomly)
        for (int i = 1; i < track.Count; i++)
        {
            var prevPoint = track[i - 1];
            var currPoint = track[i];
            
            // Calculate approximate distance between consecutive points
            var latDiff = Math.Abs(currPoint.Latitude - prevPoint.Latitude);
            var lonDiff = Math.Abs(currPoint.Longitude - prevPoint.Longitude);
            
            // Consecutive points should be relatively close (not more than ~1 degree apart)
            // This ensures they form a path rather than random scattered points
            latDiff.Should().BeLessThan(1.0, "consecutive track points should form a path");
            lonDiff.Should().BeLessThan(1.0, "consecutive track points should form a path");
        }
    }

    [Fact]
    public async Task GetVesselTrackAsync_WithUnknownMmsi_ReturnsEmpty()
    {
        // Arrange
        var client = CreateClient();
        var unknownMmsi = "999999999";

        // Act
        var result = await client.GetVesselTrackAsync(unknownMmsi);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVesselTrackAsync_DifferentHours_GeneratesCorrectTimeSpan()
    {
        // Arrange
        var client = CreateClient();
        var mmsi = "311000001";

        // Test different time periods
        var testCases = new[] { 6, 12, 24, 48 };

        foreach (var hours in testCases)
        {
            // Act
            var result = await client.GetVesselTrackAsync(mmsi, hours);

            // Assert
            result.Success.Should().BeTrue();
            var track = result.Value!.ToList();
            
            var firstPoint = track.First();
            var lastPoint = track.Last();
            var timeDifference = lastPoint.Timestamp - firstPoint.Timestamp;
            
            timeDifference.TotalHours.Should().BeApproximately(
                hours, 
                2.0, 
                $"track for {hours} hours should span approximately that time period");
        }
    }

    [Fact]
    public async Task GetVesselTrackAsync_WithInvalidHours_UsesDefaultValue()
    {
        // Arrange
        var client = CreateClient();
        var mmsi = "311000001";

        // Test invalid hours values
        var invalidHours = new[] { -1, 0, 200 }; // negative, zero, too large

        foreach (var hours in invalidHours)
        {
            // Act
            var result = await client.GetVesselTrackAsync(mmsi, hours);

            // Assert
            result.Success.Should().BeTrue();
            var track = result.Value!.ToList();
            
            // Should use default 24 hours when invalid
            var firstPoint = track.First();
            var lastPoint = track.Last();
            var timeDifference = lastPoint.Timestamp - firstPoint.Timestamp;
            
            timeDifference.TotalHours.Should().BeApproximately(
                24, 
                2.0, 
                $"invalid hours value {hours} should default to 24 hours");
        }
    }

    [Fact]
    public async Task GetVesselPositionsNearAsync_WhenNotConfigured_FiltersCorrectly()
    {
        // Arrange
        var client = CreateClient();
        var lon = -77.35; // Near BAHAMAS EXPLORER demo vessel
        var lat = 25.05;
        var radiusKm = 50;

        // Act
        var result = await client.GetVesselPositionsNearAsync(lon, lat, radiusKm);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should return at least the BAHAMAS EXPLORER which is near these coordinates
        result.Value.Should().NotBeEmpty();
    }
}
