using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using CoralLedger.Blue.Web.Endpoints.Auth;
using CoralLedger.Blue.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.IntegrationTests;

public class EmailVerificationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public EmailVerificationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_SendsVerificationEmail_AndCreatesToken()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"verify_{Guid.NewGuid():N}@example.com",
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify token was created
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();

        var token = await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id);
        token.Should().NotBeNull();
        token!.IsUsed.Should().BeFalse();
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_ConfirmsEmail()
    {
        // Arrange - Register a user first
        var email = $"verify_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Get the verification token from database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        var token = await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        // Act - Verify email
        var verifyRequest = new VerifyEmailRequest(Token: token!.Token);
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify email is confirmed
        user.Should().NotBeNull();
        if (user == null) throw new InvalidOperationException("User should not be null");
        
        await context.Entry(user).ReloadAsync();
        user.EmailConfirmed.Should().BeTrue();

        // Verify token is marked as used
        token.Should().NotBeNull();
        if (token == null) throw new InvalidOperationException("Token should not be null");
        
        await context.Entry(token).ReloadAsync();
        token.IsUsed.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var verifyRequest = new VerifyEmailRequest(Token: "invalid-token-that-does-not-exist");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_WithUsedToken_ReturnsBadRequest()
    {
        // Arrange - Register and verify a user
        var email = $"verify_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Get the verification token
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        var token = await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        // Verify email first time
        var verifyRequest = new VerifyEmailRequest(Token: token!.Token);
        await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Act - Try to verify again with same token
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredToken_ReturnsBadRequest()
    {
        // Arrange - Register a user
        var email = $"verify_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Manually expire the token
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        var token = await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        // Use reflection to set ExpiresAt to a past date (since it's private set)
        var expiresAtProperty = typeof(EmailVerificationToken).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(token, DateTime.UtcNow.AddHours(-1));
        await context.SaveChangesAsync();

        // Act - Try to verify with expired token
        var verifyRequest = new VerifyEmailRequest(Token: token!.Token);
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendVerificationEmail_WithValidEmail_SendsEmail()
    {
        // Arrange - Register a user (which creates unverified account)
        var email = $"resend_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Request to resend verification email
        var sendRequest = new SendVerificationEmailRequest(
            Email: email,
            TenantId: _factory.DefaultTenantId);
        var response = await _client.PostAsJsonAsync("/api/auth/send-verification-email", sendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify a new token was created (old one should be deleted)
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        
        var tokens = await context.EmailVerificationTokens
            .Where(t => t.UserId == user!.Id)
            .ToListAsync();
        
        tokens.Should().HaveCount(1, "old tokens should be replaced with new one");
        tokens[0].IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task SendVerificationEmail_WithNonExistentEmail_ReturnsGenericMessage()
    {
        // Arrange
        var sendRequest = new SendVerificationEmailRequest(
            Email: "nonexistent@example.com",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/send-verification-email", sendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "should return OK to prevent email enumeration");
    }

    [Fact]
    public async Task SendVerificationEmail_WithAlreadyVerifiedEmail_ReturnsBadRequest()
    {
        // Arrange - Register and verify a user
        var email = $"verified_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Verify the email
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        var token = await context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        var verifyRequest = new VerifyEmailRequest(Token: token!.Token);
        await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Act - Try to send verification email again
        var sendRequest = new SendVerificationEmailRequest(
            Email: email,
            TenantId: _factory.DefaultTenantId);
        var response = await _client.PostAsJsonAsync("/api/auth/send-verification-email", sendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendVerificationEmail_WithMissingEmail_ReturnsBadRequest()
    {
        // Arrange
        var sendRequest = new SendVerificationEmailRequest(
            Email: "",
            TenantId: _factory.DefaultTenantId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/send-verification-email", sendRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_WithMissingToken_ReturnsBadRequest()
    {
        // Arrange
        var verifyRequest = new VerifyEmailRequest(Token: "");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
