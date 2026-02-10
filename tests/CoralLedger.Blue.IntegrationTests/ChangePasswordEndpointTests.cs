using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoralLedger.Blue.Web.Endpoints.Auth;
using FluentAssertions;

namespace CoralLedger.Blue.IntegrationTests;

public class ChangePasswordEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChangePasswordEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient Client, string Email, string Password)> GetAuthenticatedClientAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        return (authenticatedClient, email, password);
    }

    [Fact]
    public async Task ChangePassword_WithValidCredentials_Succeeds()
    {
        // Arrange
        var (client, email, currentPassword) = await GetAuthenticatedClientAsync();
        var newPassword = "NewSecurePass456";

        var request = new ChangePasswordRequest(currentPassword, newPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify can login with new password
        var loginRequest = new LoginRequest(email, newPassword, _factory.DefaultTenantId);
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var (client, _, _) = await GetAuthenticatedClientAsync();

        var request = new ChangePasswordRequest("WrongPassword123", "NewSecurePass456");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("incorrect");
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var (client, _, currentPassword) = await GetAuthenticatedClientAsync();

        // Weak password - no uppercase
        var request = new ChangePasswordRequest(currentPassword, "weakpassword123");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("uppercase");
    }

    [Fact]
    public async Task ChangePassword_WithShortNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var (client, _, currentPassword) = await GetAuthenticatedClientAsync();

        // Too short password
        var request = new ChangePasswordRequest(currentPassword, "Ab1");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("8 characters");
    }

    [Fact]
    public async Task ChangePassword_WithSamePassword_ReturnsBadRequest()
    {
        // Arrange
        var (client, _, currentPassword) = await GetAuthenticatedClientAsync();

        // Same password
        var request = new ChangePasswordRequest(currentPassword, currentPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("different");
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange - Create a client that doesn't follow redirects
        var clientOptions = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        };
        var noRedirectClient = _factory.CreateClient(clientOptions);

        var request = new ChangePasswordRequest("CurrentPass123", "NewSecurePass456");

        // Act
        var response = await noRedirectClient.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task ChangePassword_WithMissingCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var (client, _, _) = await GetAuthenticatedClientAsync();

        var request = new ChangePasswordRequest("", "NewSecurePass456");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithMissingNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var (client, _, currentPassword) = await GetAuthenticatedClientAsync();

        var request = new ChangePasswordRequest(currentPassword, "");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
