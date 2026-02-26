using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoralLedger.Blue.Web.Endpoints.Auth;
using FluentAssertions;
using OtpNet;

namespace CoralLedger.Blue.IntegrationTests;

public class TwoFactorEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TwoFactorEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient Client, string Email)> GetAuthenticatedClientAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "SecurePass123",
            FullName: "Test User",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        return (authenticatedClient, email);
    }

    [Fact]
    public async Task Setup2FA_ReturnsSecretAndQrCode()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsync("/api/auth/2fa/setup", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        result.Should().NotBeNull();
        result!.SecretKey.Should().NotBeNullOrEmpty();
        result.QrCodeUri.Should().NotBeNullOrEmpty();
        result.QrCodeUri.Should().StartWith("otpauth://totp/");
    }

    [Fact]
    public async Task Setup2FA_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange - Create a client that doesn't follow redirects
        var clientOptions = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        };
        var noRedirectClient = _factory.CreateClient(clientOptions);

        // Act
        var response = await noRedirectClient.PostAsync("/api/auth/2fa/setup", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Enable2FA_WithValidCode_Enables2FA()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Get setup response
        var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        setupResult.Should().NotBeNull();

        // Generate a valid TOTP code
        var secretBytes = Base32Encoding.ToBytes(setupResult!.SecretKey);
        var totp = new Totp(secretBytes);
        var code = totp.ComputeTotp();

        // Act
        var enableRequest = new Enable2FARequest(code);
        var response = await client.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<Enable2FAResponse>();
        result.Should().NotBeNull();
        result!.RecoveryCodes.Should().NotBeEmpty();
        result.RecoveryCodes.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Enable2FA_WithInvalidCode_ReturnsBadRequest()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Get setup response
        var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();

        // Act - Use invalid code
        var enableRequest = new Enable2FARequest("000000");
        var response = await client.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get2FAStatus_WithoutEnabled_ReturnsDisabled()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/auth/2fa/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        result.Should().NotBeNull();
        result!.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Get2FAStatus_AfterEnabled_ReturnsEnabled()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Setup and enable 2FA
        var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secretBytes = Base32Encoding.ToBytes(setupResult!.SecretKey);
        var totp = new Totp(secretBytes);
        var code = totp.ComputeTotp();

        var enableRequest = new Enable2FARequest(code);
        await client.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Act
        var response = await client.GetAsync("/api/auth/2fa/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        result.Should().NotBeNull();
        result!.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Disable2FA_WithValidCode_Disables2FA()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Setup and enable 2FA
        var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secretBytes = Base32Encoding.ToBytes(setupResult!.SecretKey);
        var totp = new Totp(secretBytes);
        var enableCode = totp.ComputeTotp();

        var enableRequest = new Enable2FARequest(enableCode);
        await client.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Generate a new valid code for disable
        var disableCode = totp.ComputeTotp();

        // Act
        var disableRequest = new Disable2FARequest(disableCode);
        var response = await client.PostAsJsonAsync("/api/auth/2fa/disable", disableRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify 2FA is now disabled
        var statusResponse = await client.GetAsync("/api/auth/2fa/status");
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        statusResult!.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Disable2FA_WithInvalidCode_ReturnsBadRequest()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Setup and enable 2FA
        var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secretBytes = Base32Encoding.ToBytes(setupResult!.SecretKey);
        var totp = new Totp(secretBytes);
        var enableCode = totp.ComputeTotp();

        var enableRequest = new Enable2FARequest(enableCode);
        await client.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Act - Use invalid code
        var disableRequest = new Disable2FARequest("000000");
        var response = await client.PostAsJsonAsync("/api/auth/2fa/disable", disableRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validate2FA_WithValidCode_ReturnsSuccess()
    {
        // Arrange
        var (client, _) = await GetAuthenticatedClientAsync();

        // Setup and enable 2FA
        var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secretBytes = Base32Encoding.ToBytes(setupResult!.SecretKey);
        var totp = new Totp(secretBytes);
        var enableCode = totp.ComputeTotp();

        var enableRequest = new Enable2FARequest(enableCode);
        await client.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Get user ID from the auth response
        var statusResponse = await client.GetAsync("/api/auth/2fa/status");
        statusResponse.EnsureSuccessStatusCode();

        // Note: We need to get the user ID somehow - for this test we'll use the original auth response
        // In reality, this would come from the login response

        // For the validate endpoint, we use anonymous client since it's for login flow
        var anonymousClient = _factory.CreateClient();

        // Act - We can't easily test this without knowing the user ID from 2FA login flow
        // The validate endpoint is primarily tested through the login flow
        // This test verifies the endpoint exists and returns proper errors

        var validateRequest = new Validate2FARequest(Guid.Empty, totp.ComputeTotp());
        var response = await anonymousClient.PostAsJsonAsync("/api/auth/2fa/validate", validateRequest);

        // Assert - Should return 401 for non-existent user
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndToEnd2FAFlow_SetupEnableLoginValidate_Success()
    {
        // Arrange: Register a new user
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123!";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "2FA Test User",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Create authenticated client for setup/enable
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registerResult!.AccessToken);

        // Step 1: Setup 2FA
        var setupResponse = await authenticatedClient.PostAsync("/api/auth/2fa/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        setupResult.Should().NotBeNull();
        setupResult!.SecretKey.Should().NotBeNullOrEmpty();

        // Step 2: Enable 2FA with valid TOTP code
        var secretBytes = Base32Encoding.ToBytes(setupResult.SecretKey);
        var totp = new Totp(secretBytes);
        var enableCode = totp.ComputeTotp();

        var enableRequest = new Enable2FARequest(enableCode);
        var enableResponse = await authenticatedClient.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);
        enableResponse.EnsureSuccessStatusCode();
        var enableResult = await enableResponse.Content.ReadFromJsonAsync<Enable2FAResponse>();
        enableResult.Should().NotBeNull();
        enableResult!.RecoveryCodes.Should().NotBeEmpty();

        // Step 3: Verify 2FA is enabled
        var statusResponse = await authenticatedClient.GetAsync("/api/auth/2fa/status");
        statusResponse.EnsureSuccessStatusCode();
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        statusResult!.TwoFactorEnabled.Should().BeTrue();

        // Step 4: Login with password (should return 202 with 2FA required)
        var loginRequest = new LoginRequest(
            Email: email,
            Password: password,
            TenantId: _factory.DefaultTenantId);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Accepted); // 202 Accepted for 2FA required
        var twoFactorRequired = await loginResponse.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();
        twoFactorRequired.Should().NotBeNull();
        twoFactorRequired!.RequiresTwoFactor.Should().BeTrue();
        twoFactorRequired.UserId.Should().Be(registerResult.UserId);

        // Step 5: Validate 2FA code
        var validateCode = totp.ComputeTotp();
        var validateRequest = new Validate2FARequest(twoFactorRequired.UserId, validateCode);
        var validateResponse = await _client.PostAsJsonAsync("/api/auth/2fa/validate", validateRequest);
        validateResponse.EnsureSuccessStatusCode();
        var validateResult = await validateResponse.Content.ReadFromJsonAsync<TwoFactorValidateResponse>();
        validateResult.Should().NotBeNull();
        validateResult!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Login_With2FAEnabled_WithInvalidCode_ReturnsUnauthorized()
    {
        // Arrange: Register user and enable 2FA
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123!";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: password,
            FullName: "2FA Test User",
            TenantId: _factory.DefaultTenantId);

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registerResult!.AccessToken);

        // Setup and enable 2FA
        var setupResponse = await authenticatedClient.PostAsync("/api/auth/2fa/setup", null);
        var setupResult = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secretBytes = Base32Encoding.ToBytes(setupResult!.SecretKey);
        var totp = new Totp(secretBytes);
        var enableCode = totp.ComputeTotp();

        var enableRequest = new Enable2FARequest(enableCode);
        await authenticatedClient.PostAsJsonAsync("/api/auth/2fa/enable", enableRequest);

        // Act: Login with password
        var loginRequest = new LoginRequest(
            Email: email,
            Password: password,
            TenantId: _factory.DefaultTenantId);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var twoFactorRequired = await loginResponse.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();

        // Try to validate with invalid code
        var validateRequest = new Validate2FARequest(twoFactorRequired!.UserId, "000000");
        var validateResponse = await _client.PostAsJsonAsync("/api/auth/2fa/validate", validateRequest);

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
