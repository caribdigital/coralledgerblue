using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoralLedger.Blue.Web.Endpoints.Auth;
using FluentAssertions;

namespace CoralLedger.Blue.IntegrationTests;

public class AdminEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<HttpClient> GetAuthenticatedAdminClientAsync()
    {
        var loginRequest = new
        {
            Email = CustomWebApplicationFactory.AdminEmail,
            Password = CustomWebApplicationFactory.AdminPassword,
            TenantId = _factory.DefaultTenantId
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        return authenticatedClient;
    }

    [Fact]
    public async Task GetAdminDashboard_ReturnsSuccessWithData()
    {
        // Arrange
        var client = await GetAuthenticatedAdminClientAsync();

        // Act
        var response = await client.GetAsync("/api/admin/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("counts");
        content.Should().Contain("recent");
    }

    [Fact]
    public async Task GetPendingObservations_ReturnsSuccessWithPaginatedData()
    {
        // Arrange
        var client = await GetAuthenticatedAdminClientAsync();

        // Act
        var response = await client.GetAsync("/api/admin/observations/pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("items");
        content.Should().Contain("total");
        content.Should().Contain("page");
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsSuccessWithStatus()
    {
        // Arrange
        var client = await GetAuthenticatedAdminClientAsync();

        // Act
        var response = await client.GetAsync("/api/admin/system/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
        content.Should().Contain("database");
    }

    [Fact]
    public async Task GetSystemConfig_ReturnsSuccessWithFeatures()
    {
        // Arrange
        var client = await GetAuthenticatedAdminClientAsync();

        // Act
        var response = await client.GetAsync("/api/admin/system/config");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("features");
    }

    [Fact]
    public async Task GetDataSummary_ReturnsSuccessWithSummary()
    {
        // Arrange
        var client = await GetAuthenticatedAdminClientAsync();

        // Act
        var response = await client.GetAsync("/api/admin/data/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("mpas");
    }

    [Fact]
    public async Task GetJobs_ReturnsSuccessWithJobList()
    {
        // Arrange
        var client = await GetAuthenticatedAdminClientAsync();

        // Act
        var response = await client.GetAsync("/api/admin/jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("jobs");
    }

    [Fact]
    public async Task AdminEndpoint_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create a client that doesn't follow redirects
        var clientOptions = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        };
        var noRedirectClient = _factory.CreateClient(clientOptions);

        // Act - Use unauthenticated client
        var response = await noRedirectClient.GetAsync("/api/admin/dashboard");

        // Assert - Should redirect to login (302) or return 401 Unauthorized
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);
    }
}
