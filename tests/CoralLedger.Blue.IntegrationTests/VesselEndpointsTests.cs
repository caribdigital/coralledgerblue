using System.Net;
using FluentAssertions;

namespace CoralLedger.Blue.IntegrationTests;

/// <summary>
/// Integration tests for Vessel API endpoints
/// Tests Global Fishing Watch integration endpoints
/// </summary>
public class VesselEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public VesselEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Search Endpoint Tests

    [Fact]
    public async Task SearchVessels_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchVessels_WithQuery_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/search?query=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchVessels_WithFlagFilter_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/search?flag=USA");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchVessels_WithLimitParameter_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/search?limit=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Fishing Events Endpoint Tests

    [Fact]
    public async Task GetFishingEvents_WithValidBounds_ReturnsOk()
    {
        // Arrange - Bahamas bounding box
        var minLon = -80.5;
        var minLat = 20.5;
        var maxLon = -72.5;
        var maxLat = 27.5;
        var startDate = DateTime.UtcNow.AddDays(-7).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/fishing-events?minLon={minLon}&minLat={minLat}&maxLon={maxLon}&maxLat={maxLat}&startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFishingEvents_ReturnsJsonContent()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/fishing-events?minLon=-80&minLat=20&maxLon=-72&maxLat=28&startDate={startDate}&endDate={endDate}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetBahamasFishingEvents_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/fishing-events/bahamas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBahamasFishingEvents_WithDateRange_ReturnsOk()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/fishing-events/bahamas?startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBahamasFishingEvents_ReturnsJsonArray()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/fishing-events/bahamas");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().StartWith("["); // JSON array
    }

    #endregion

    #region Fishing Effort Stats Endpoint Tests

    [Fact]
    public async Task GetFishingEffortStats_WithValidBounds_ReturnsOk()
    {
        // Arrange - Bahamas bounding box
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/stats?minLon=-80&minLat=20&maxLon=-72&maxLat=28&startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFishingEffortStats_ReturnsExpectedFields()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/stats?minLon=-80&minLat=20&maxLon=-72&maxLat=28&startDate={startDate}&endDate={endDate}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("totalFishingHours");
        content.Should().Contain("vesselCount");
    }

    #endregion

    #region Fishing Effort Tiles Endpoint Tests

    [Fact]
    public async Task GetFishingEffortTileUrl_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/fishing-effort-tiles");

        // Assert - May be OK or NotFound depending on GFW API availability
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFishingEffortTileUrl_WithDateRange_ReturnsOk()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/fishing-effort-tiles?startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFishingEffortTileUrl_WithFilters_ReturnsOk()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/fishing-effort-tiles?startDate={startDate}&endDate={endDate}&gearType=tuna_purse_seines&flag=ESP");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Encounters Endpoint Tests

    [Fact]
    public async Task GetEncounters_WithValidBounds_ReturnsOk()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var endDate = DateTime.UtcNow.ToString("O");

        // Act
        var response = await _client.GetAsync(
            $"/api/vessels/encounters?minLon=-80&minLat=20&maxLon=-72&maxLat=28&startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Vessel by ID Endpoint Tests

    [Fact]
    public async Task GetVesselById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/vessels/invalid-vessel-id-12345");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
