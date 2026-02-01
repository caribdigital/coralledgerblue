using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using CoralLedger.Blue.Web.Endpoints;

namespace CoralLedger.Blue.IntegrationTests;

public class ApiKeyManagementEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiKeyManagementEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateApiClient_ReturnsSuccessWithApiKey()
    {
        // Arrange
        var request = new CreateApiClientRequest(
            Name: "Test Client",
            OrganizationName: "Test Organization",
            Description: "Integration test client",
            ContactEmail: "test@example.com",
            RateLimitPerMinute: 60
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/api-keys/clients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content));
    }

    [Fact]
    public async Task GetApiClients_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/api-keys/clients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateApiClient_ThenGetById_ReturnsClient()
    {
        // Arrange - Create a client first
        var createRequest = new CreateApiClientRequest(
            Name: "Test Client for GetById",
            OrganizationName: "Test Org",
            ContactEmail: "test@example.com"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var createDoc = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var clientId = createDoc.RootElement.GetProperty("client").GetProperty("id").GetString();
        Assert.NotNull(clientId);

        // Act - Get the client by ID
        var getResponse = await _client.GetAsync($"/api/api-keys/clients/{clientId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var getDoc = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        var name = getDoc.RootElement.GetProperty("name").GetString();
        Assert.Equal("Test Client for GetById", name);
    }

    [Fact]
    public async Task GetApiClient_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/api-keys/clients/{invalidId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateApiKey_ForExistingClient_ReturnsSuccess()
    {
        // Arrange - Create a client first
        var createClientRequest = new CreateApiClientRequest(
            Name: "Client for New Key",
            ContactEmail: "test@example.com"
        );

        var createClientResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createClientRequest);
        using var createClientDoc = await JsonDocument.ParseAsync(await createClientResponse.Content.ReadAsStreamAsync());
        var clientId = createClientDoc.RootElement.GetProperty("client").GetProperty("id").GetString();

        var createKeyRequest = new CreateApiKeyRequest(
            Name: "Additional Key",
            Scopes: "read,write"
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/api-keys/clients/{clientId}/keys", createKeyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var resultDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(resultDoc.RootElement.TryGetProperty("apiKey", out _));
        Assert.True(resultDoc.RootElement.TryGetProperty("plainKey", out _));
    }

    [Fact]
    public async Task RevokeApiKey_WithValidKey_ReturnsSuccess()
    {
        // Arrange - Create a client with a key
        var createRequest = new CreateApiClientRequest(
            Name: "Client for Revocation Test",
            ContactEmail: "test@example.com"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createRequest);
        using var createDoc = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var apiKeyId = createDoc.RootElement.GetProperty("apiKey").GetProperty("id").GetString();
        Assert.NotNull(apiKeyId);

        var revokeRequest = new RevokeApiKeyRequest("Test revocation");

        // Act - Use POST to revoke endpoint
        var revokeResponse = await _client.PostAsJsonAsync($"/api/api-keys/{apiKeyId}/revoke", revokeRequest);

        // Assert
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsageStatistics_ForClient_ReturnsStatistics()
    {
        // Arrange - Create a client
        var createRequest = new CreateApiClientRequest(
            Name: "Client for Usage Stats",
            ContactEmail: "test@example.com"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createRequest);
        using var createDoc = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var clientId = createDoc.RootElement.GetProperty("client").GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/api/api-keys/clients/{clientId}/usage");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsageLogs_ForClient_ReturnsLogs()
    {
        // Arrange - Create a client
        var createRequest = new CreateApiClientRequest(
            Name: "Client for Usage Logs",
            ContactEmail: "test@example.com"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/api-keys/clients", createRequest);
        using var createDoc = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var clientId = createDoc.RootElement.GetProperty("client").GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/api/api-keys/clients/{clientId}/logs?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
