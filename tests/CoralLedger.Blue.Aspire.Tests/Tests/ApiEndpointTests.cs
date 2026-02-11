using System.Net;
using System.Text.Json;

namespace CoralLedger.Blue.Aspire.Tests.Tests;

/// <summary>
/// Tests for API endpoints
/// </summary>
[Collection("Aspire")]
public class ApiEndpointTests
{
    private readonly AspireIntegrationFixture _fixture;

    public ApiEndpointTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MpaEndpoint_ReturnsData()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/mpas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BleachingEndpoint_ReturnsData()
    {
        // Act - Use bahamas endpoint which has data without parameters
        var response = await _fixture.WebClient.GetAsync("/api/bleaching/bahamas");

        // Assert - Accept success or gateway/service unavailable errors (external NOAA API may be down)
        // Note: 500 Internal Server Error removed from acceptable codes as it indicates an API bug,
        // not an upstream dependency failure. Only gateway-level errors (502/503/504) are acceptable.
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway,           // 502: Bad Gateway
            HttpStatusCode.ServiceUnavailable,   // 503: Service Unavailable
            HttpStatusCode.GatewayTimeout);      // 504: Gateway Timeout

        if (response.IsSuccessStatusCode)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        }
    }

    [Fact]
    public async Task VesselEndpoint_ReturnsData()
    {
        // Act - Use search endpoint which works without required parameters
        var response = await _fixture.WebClient.GetAsync("/api/vessels/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SpeciesEndpoint_ReturnsData()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/species");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task AlertsEndpoint_ReturnsData()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/alerts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task ObservationsEndpoint_ReturnsData()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/observations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task JobsEndpoint_ReturnsJobStatus()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/jobs/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
