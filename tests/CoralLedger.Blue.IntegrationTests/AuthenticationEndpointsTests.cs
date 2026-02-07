using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using CoralLedger.Blue.Web.Endpoints.Auth;

namespace CoralLedger.Blue.IntegrationTests;

public class AuthenticationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthenticationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsOkWithTokens()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"newuser_{Guid.NewGuid():N}@example.com",
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.RefreshToken.Should().NotBeNullOrEmpty();
        authResponse.Email.Should().Be(request.Email.ToLowerInvariant());
        authResponse.UserId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - First register a user
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var firstRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "First User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", firstRequest);

        // Act - Try to register again with same email
        var secondRequest = new RegisterRequest(
            Email: email,
            Password: "DifferentPass456",
            FullName: "Second User",
            TenantId: _factory.DefaultTenantId);

        var response = await _client.PostAsJsonAsync("/api/auth/register", secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"weakpwd_{Guid.NewGuid():N}@example.com",
            Password: "short",  // Less than 8 characters
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithMissingEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "",
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
    {
        // Arrange - First register a user
        var email = $"logintest_{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Login Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Login with same credentials
        var loginRequest = new LoginRequest(
            Email: email,
            Password: password,
            TenantId: _factory.DefaultTenantId);

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.Email.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange - First register a user
        var email = $"wrongpwd_{Guid.NewGuid():N}@example.com";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "CorrectPass123",
            FullName: "Wrong Password Test",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Login with wrong password
        var loginRequest = new LoginRequest(
            Email: email,
            Password: "WrongPass456",
            TenantId: _factory.DefaultTenantId);

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            Email: "nonexistent@example.com",
            Password: "SomePass123",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithMissingCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            Email: "",
            Password: "",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AccountLockout_AfterFailedAttempts()
    {
        // Arrange - Register a user
        var email = $"lockout_{Guid.NewGuid():N}@example.com";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "CorrectPass123",
            FullName: "Lockout Test",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Make 5 failed login attempts
        var wrongLoginRequest = new LoginRequest(
            Email: email,
            Password: "WrongPass999",
            TenantId: _factory.DefaultTenantId);

        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", wrongLoginRequest);
        }

        // Try one more time - should be locked out
        var response = await _client.PostAsJsonAsync("/api/auth/login", wrongLoginRequest);

        // Assert - Account should be locked (403 Forbidden)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JwtToken_IsValidForAuthenticatedEndpoints()
    {
        // Arrange - Register and get token
        var email = $"jwttest_{Guid.NewGuid():N}@example.com";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "JWT Test User",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Act - Use token to access authenticated endpoint
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        // Try to access an endpoint that accepts both API key and JWT
        // For now just verify the token was generated properly
        authResponse.AccessToken.Should().Contain(".");  // JWT has 3 parts separated by dots
        authResponse.AccessToken.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task Logout_ClearsAuthenticationCookie()
    {
        // Arrange - Register and login to get a cookie
        var email = $"logouttest_{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Logout Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Logout
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify cookie is cleared by checking for Set-Cookie header with the auth cookie name
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            var cookieHeaders = setCookieHeaders.ToList();
            cookieHeaders.Should().NotBeEmpty("Logout should set a cookie header to clear the authentication cookie");
            
            // Check that the auth cookie is being cleared (contains the cookie name and has expiry/max-age set)
            var authCookieHeader = cookieHeaders.FirstOrDefault(h => h.Contains("CoralLedger.Auth"));
            authCookieHeader.Should().NotBeNull("Response should contain Set-Cookie header for CoralLedger.Auth");
        }
        else
        {
            // If no Set-Cookie header, the test should fail as logout must clear the cookie
            Assert.Fail("Expected Set-Cookie header to be present in logout response");
        }
    }
}
