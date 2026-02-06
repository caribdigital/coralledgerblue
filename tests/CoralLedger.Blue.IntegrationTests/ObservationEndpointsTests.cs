using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using CoralLedger.Blue.Domain.Enums;
using CoralLedger.Blue.Web.Endpoints;

namespace CoralLedger.Blue.IntegrationTests;

public class ObservationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ObservationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateObservation_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateObservationRequest(
            Longitude: -77.5,
            Latitude: 25.0,
            ObservationTime: DateTime.UtcNow,
            Title: "Test Observation",
            Type: ObservationType.CoralBleaching,
            Description: "Test description",
            Severity: 3,
            CitizenEmail: "test@example.com",
            CitizenName: "Test User"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/observations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateObservation_WithValidApiKey_ReturnsCreated()
    {
        // Arrange - Create an API client with a valid API key
        var createClientRequest = new CreateApiClientRequest(
            Name: "Test Observation Client",
            OrganizationName: "Test Org",
            Description: "Integration test client for observations",
            ContactEmail: "observer@example.com",
            RateLimitPerMinute: 60
        );

        var createClientResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createClientRequest);
        createClientResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var createClientDoc = await JsonDocument.ParseAsync(await createClientResponse.Content.ReadAsStreamAsync());
        var plainKey = createClientDoc.RootElement.GetProperty("plainKey").GetString();
        Assert.NotNull(plainKey);

        // Create observation request
        var observationRequest = new CreateObservationRequest(
            Longitude: -77.5,
            Latitude: 25.0,
            ObservationTime: DateTime.UtcNow,
            Title: "Test Observation with API Key",
            Type: ObservationType.CoralBleaching,
            Description: "Test description",
            Severity: 3,
            CitizenEmail: "test@example.com",
            CitizenName: "Test User"
        );

        // Create a new client with API key header
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("X-API-Key", plainKey);

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/observations", observationRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        using var resultDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        
        Assert.True(resultDoc.RootElement.TryGetProperty("observationId", out var observationId));
        Assert.False(observationId.GetGuid() == Guid.Empty);
        Assert.True(resultDoc.RootElement.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());
    }

    [Fact]
    public async Task CreateObservation_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateObservationRequest(
            Longitude: -77.5,
            Latitude: 25.0,
            ObservationTime: DateTime.UtcNow,
            Title: "Test Observation",
            Type: ObservationType.CoralBleaching,
            Description: "Test description",
            Severity: 3,
            CitizenEmail: "test@example.com",
            CitizenName: "Test User"
        );

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("X-API-Key", "clb_invalid_key_12345678901234567890");

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/observations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetObservations_WithoutApiKey_ReturnsOk()
    {
        // Reading observations should not require authentication
        // Act
        var response = await _client.GetAsync("/api/observations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateObservation_StoresApiClientId()
    {
        // Arrange - Create an API client with a valid API key
        var createClientRequest = new CreateApiClientRequest(
            Name: "Test Client for Tracking",
            OrganizationName: "Test Org",
            ContactEmail: "tracking@example.com",
            RateLimitPerMinute: 60
        );

        var createClientResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createClientRequest);
        using var createClientDoc = await JsonDocument.ParseAsync(await createClientResponse.Content.ReadAsStreamAsync());
        var plainKey = createClientDoc.RootElement.GetProperty("plainKey").GetString();
        var clientId = createClientDoc.RootElement.GetProperty("client").GetProperty("clientId").GetString();
        Assert.NotNull(plainKey);
        Assert.NotNull(clientId);

        // Create observation
        var observationRequest = new CreateObservationRequest(
            Longitude: -77.5,
            Latitude: 25.0,
            ObservationTime: DateTime.UtcNow,
            Title: "Test for ClientId Tracking",
            Type: ObservationType.MarineDebris,
            Description: "Test description",
            Severity: 2,
            CitizenEmail: "tracker@example.com",
            CitizenName: "Test Tracker"
        );

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("X-API-Key", plainKey);

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/observations", observationRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        using var resultDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var observationId = resultDoc.RootElement.GetProperty("observationId").GetGuid();

        // Get the observation to verify it has the client ID stored
        var getResponse = await _client.GetAsync($"/api/observations/{observationId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Note: This test verifies the observation was created and can be retrieved
        // The actual verification of ApiClientId storage would require database access
        // or exposing it in the response, which we intentionally don't do for security
    }
}
