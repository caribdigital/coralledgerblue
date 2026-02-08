using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using CoralLedger.Blue.Web.Endpoints.Auth;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.IntegrationTests;

public class RefreshTokenEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public RefreshTokenEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange - Register and get initial tokens
        var email = $"refresh_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Refresh Test User",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        authResponse.Should().NotBeNull();
        var originalRefreshToken = authResponse!.RefreshToken;
        var originalAccessToken = authResponse.AccessToken;

        // Act - Use refresh token to get new tokens
        var refreshRequest = new RefreshTokenRequest(originalRefreshToken);
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAuthResponse = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        newAuthResponse.Should().NotBeNull();
        newAuthResponse!.AccessToken.Should().NotBeNullOrEmpty();
        newAuthResponse.RefreshToken.Should().NotBeNullOrEmpty();
        
        // Refresh token must be different (token rotation)
        newAuthResponse.RefreshToken.Should().NotBe(originalRefreshToken);
        
        // User info should match
        newAuthResponse.Email.Should().Be(email.ToLowerInvariant());
        newAuthResponse.UserId.Should().Be(authResponse.UserId);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest("invalid-refresh-token");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithEmptyToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest("");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_TokenRotation_OldTokenBecomesInvalid()
    {
        // Arrange - Register and get initial tokens
        var email = $"rotation_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Token Rotation Test",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var originalRefreshToken = authResponse!.RefreshToken;

        // Act - Use refresh token once
        var firstRefreshRequest = new RefreshTokenRequest(originalRefreshToken);
        var firstRefreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", firstRefreshRequest);
        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to use the same token again
        var secondRefreshRequest = new RefreshTokenRequest(originalRefreshToken);
        var secondRefreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", secondRefreshRequest);

        // Assert - Second attempt should fail (token rotation)
        secondRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_StoresRefreshTokenInDatabase()
    {
        // Arrange - Register a user first
        var email = $"dbstore_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "DB Store Test",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Login to get new tokens
        var loginRequest = new LoginRequest(
            Email: email,
            Password: password,
            TenantId: _factory.DefaultTenantId);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Assert - Verify token is stored in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await dbContext.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        user.Should().NotBeNull();

        var refreshTokens = await dbContext.RefreshTokens
            .Where(t => t.TenantUserId == user!.Id)
            .ToListAsync();

        // Should have at least 2 tokens (one from register, one from login)
        refreshTokens.Should().NotBeEmpty();
        refreshTokens.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Logout_RevokesAllUserRefreshTokens()
    {
        // Arrange - Register and get tokens
        var email = $"logout_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Logout Test",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var refreshToken = authResponse!.RefreshToken;

        // Act - Logout
        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to use refresh token after logout
        var refreshRequest = new RefreshTokenRequest(refreshToken);
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert - Refresh token should no longer work
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithInactiveUser_ReturnsUnauthorized()
    {
        // Arrange - Register and get tokens
        var email = $"inactive_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Inactive User Test",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var refreshToken = authResponse!.RefreshToken;

        // Deactivate user
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
            var user = await dbContext.TenantUsers
                .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
            user!.Deactivate();
            await dbContext.SaveChangesAsync();
        }

        // Act - Try to use refresh token for inactive user
        var refreshRequest = new RefreshTokenRequest(refreshToken);
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_MultipleRefreshes_CreatesDifferentTokens()
    {
        // Arrange - Register and get initial tokens
        var email = $"multirefresh_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "Multi Refresh Test",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Act - Perform multiple refreshes
        var tokens = new List<string> { authResponse!.RefreshToken };
        
        for (int i = 0; i < 3; i++)
        {
            var refreshRequest = new RefreshTokenRequest(tokens[^1]);
            var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
            var newAuthResponse = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
            tokens.Add(newAuthResponse!.RefreshToken);
        }

        // Assert - All tokens should be unique
        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().HaveCount(4); // 1 original + 3 refreshed
    }
}
