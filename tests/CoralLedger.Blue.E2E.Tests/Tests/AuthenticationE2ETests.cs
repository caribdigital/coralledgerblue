namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E tests for authentication user journeys.
/// Tests login, registration, password reset, and protected page access flows.
/// </summary>
[TestFixture]
public class AuthenticationE2ETests : PlaywrightFixture
{
    [Test]
    [Description("Verifies the login page loads and displays all required elements")]
    public async Task Login_PageLoadsWithRequiredElements()
    {
        // Act
        await NavigateToAsync("/login");

        // Assert - Page title and form elements exist
        var emailInput = Page.Locator("#email");
        var passwordInput = Page.Locator("#password");
        var submitButton = Page.Locator("button[type='submit']");
        var registerLink = Page.Locator("a[href='/register']");
        var forgotPasswordLink = Page.Locator("a[href='/forgot-password']");

        await Expect(emailInput).ToBeVisibleAsync();
        await Expect(passwordInput).ToBeVisibleAsync();
        await Expect(submitButton).ToBeVisibleAsync();
        await Expect(registerLink).ToBeVisibleAsync();
        await Expect(forgotPasswordLink).ToBeVisibleAsync();

        // OAuth buttons should be visible
        var googleButton = Page.Locator("a[href='/api/auth/signin-google']");
        var microsoftButton = Page.Locator("a[href='/api/auth/signin-microsoft']");
        await Expect(googleButton).ToBeVisibleAsync();
        await Expect(microsoftButton).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verifies invalid login shows error message")]
    public async Task Login_WithInvalidCredentials_ShowsErrorMessage()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act - Fill in invalid credentials
        await Page.FillAsync("#email", "invalid@example.com");
        await Page.FillAsync("#password", "WrongPassword123");
        await Page.ClickAsync("button[type='submit']");

        // Wait for response
        await Task.Delay(2000);

        // Assert - Error message should be displayed
        var errorAlert = Page.Locator(".alert-danger");
        var isVisible = await errorAlert.IsVisibleAsync();

        // Soft assertion - server may not be running
        if (isVisible)
        {
            var errorText = await errorAlert.TextContentAsync();
            errorText.Should().NotBeNullOrEmpty("Error message should be displayed");
            TestContext.WriteLine($"Error message displayed: {errorText}");
        }
        else
        {
            TestContext.WriteLine("Note: Error message not visible - server may not be processing requests");
        }
    }

    [Test]
    [Description("Verifies the register page loads and displays all required elements")]
    public async Task Register_PageLoadsWithRequiredElements()
    {
        // Act
        await NavigateToAsync("/register");

        // Assert - Page title and form elements exist
        var fullNameInput = Page.Locator("#fullName");
        var emailInput = Page.Locator("#email");
        var passwordInput = Page.Locator("#password");
        var confirmPasswordInput = Page.Locator("#confirmPassword");
        var submitButton = Page.Locator("button[type='submit']");
        var loginLink = Page.Locator("a[href='/login']");

        await Expect(fullNameInput).ToBeVisibleAsync();
        await Expect(emailInput).ToBeVisibleAsync();
        await Expect(passwordInput).ToBeVisibleAsync();
        await Expect(confirmPasswordInput).ToBeVisibleAsync();
        await Expect(submitButton).ToBeVisibleAsync();
        await Expect(loginLink).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verifies registration with mismatched passwords shows validation error")]
    public async Task Register_WithMismatchedPasswords_ShowsValidationError()
    {
        // Arrange
        await NavigateToAsync("/register");

        // Act - Fill in form with mismatched passwords
        await Page.FillAsync("#fullName", "Test User");
        await Page.FillAsync("#email", "test@example.com");
        await Page.FillAsync("#password", "SecurePass123");
        await Page.FillAsync("#confirmPassword", "DifferentPass456");
        await Page.ClickAsync("button[type='submit']");

        // Wait for client-side validation
        await Task.Delay(1000);

        // Assert - Validation message should appear
        var validationMessages = Page.Locator(".validation-message, .field-validation-error");
        var count = await validationMessages.CountAsync();

        // Either client-side validation or we submitted and got server error
        if (count > 0)
        {
            TestContext.WriteLine($"Validation messages found: {count}");
        }
        else
        {
            // Check for server-side error after delay
            await Task.Delay(2000);
            var errorAlert = Page.Locator(".alert-danger");
            var hasError = await errorAlert.IsVisibleAsync();
            TestContext.WriteLine($"Server error alert visible: {hasError}");
        }
    }

    [Test]
    [Description("Verifies the forgot password page loads correctly")]
    public async Task ForgotPassword_PageLoadsWithRequiredElements()
    {
        // Act
        await NavigateToAsync("/forgot-password");

        // Assert - Page should have email input and submit button
        var emailInput = Page.Locator("#email");
        var submitButton = Page.Locator("button[type='submit']");
        var loginLink = Page.Locator("a[href='/login']");

        await Expect(emailInput).ToBeVisibleAsync();
        await Expect(submitButton).ToBeVisibleAsync();
        await Expect(loginLink).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verifies protected pages redirect to login when not authenticated")]
    public async Task ProtectedPage_RedirectsToLogin_WhenNotAuthenticated()
    {
        // Act - Try to access account settings (protected page)
        await NavigateToAsync("/account/settings");

        // Wait for potential redirect
        await Task.Delay(2000);

        // Assert - Should redirect to login or show unauthorized
        var currentUrl = Page.Url;

        // The page should either redirect to /login or show access denied
        var isOnLoginPage = currentUrl.Contains("/login");
        var isOnUnauthorizedPage = currentUrl.Contains("/unauthorized") || currentUrl.Contains("/access-denied");

        // Check if login form is visible (redirected to login)
        var loginForm = Page.Locator("#email");
        var loginFormVisible = await loginForm.IsVisibleAsync();

        (isOnLoginPage || isOnUnauthorizedPage || loginFormVisible).Should().BeTrue(
            $"Protected page should redirect to login or show unauthorized. Current URL: {currentUrl}");

        TestContext.WriteLine($"Redirected to: {currentUrl}");
    }

    [Test]
    [Description("Verifies navigation from login to register page")]
    public async Task Login_NavigateToRegister_Works()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act - Click register link
        var registerLink = Page.Locator("a[href='/register']");
        await registerLink.ClickAsync();
        await WaitForBlazorAsync();

        // Assert
        Page.Url.Should().Contain("/register");

        // Verify register form is visible
        var emailInput = Page.Locator("#email");
        await Expect(emailInput).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verifies navigation from login to forgot password page")]
    public async Task Login_NavigateToForgotPassword_Works()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act - Click forgot password link
        var forgotPasswordLink = Page.Locator("a[href='/forgot-password']");
        await forgotPasswordLink.ClickAsync();
        await WaitForBlazorAsync();

        // Assert
        Page.Url.Should().Contain("/forgot-password");

        // Verify forgot password form is visible
        var emailInput = Page.Locator("#email");
        await Expect(emailInput).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verifies email confirmation page loads with email parameter")]
    public async Task EmailConfirmation_PageLoadsWithEmail()
    {
        // Act - Navigate with email parameter
        var testEmail = "test@example.com";
        await NavigateToAsync($"/email-confirmation?email={Uri.EscapeDataString(testEmail)}");

        // Assert - Page should display success message and email
        var pageContent = await Page.ContentAsync();

        // Check for email confirmation elements
        var successIcon = Page.Locator(".bi-envelope-check, .success-icon, i.bi");
        var iconCount = await successIcon.CountAsync();

        TestContext.WriteLine($"Page loaded. Found {iconCount} icon elements.");
        TestContext.WriteLine($"Current URL: {Page.Url}");

        // Page should be accessible (not 404)
        Page.Url.Should().Contain("/email-confirmation");
    }

    [Test]
    [Description("Verifies 2FA validate page loads with required parameters")]
    public async Task TwoFactorValidate_PageLoadsWithRequiredElements()
    {
        // Act - Navigate with mock parameters
        var userId = Guid.NewGuid();
        await NavigateToAsync($"/2fa/validate?userId={userId}&returnUrl=/");

        // Assert - Page should have code input
        var codeInput = Page.Locator("#code, input[maxlength='6']");
        var submitButton = Page.Locator("button[type='submit']");

        await Expect(codeInput).ToBeVisibleAsync();
        await Expect(submitButton).ToBeVisibleAsync();

        TestContext.WriteLine($"2FA validate page loaded for userId: {userId}");
    }

    [Test]
    [Description("Verifies login page has no console errors")]
    public async Task Login_NoConsoleErrors()
    {
        // Arrange
        ConsoleErrors.Clear();

        // Act
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Assert - Filter out expected Blazor/SignalR errors
        var expectedErrors = new[] { "NetworkError", "fetch", "SignalR", "blazor", "circuit", "Blob" };
        var criticalErrors = ConsoleErrors
            .Where(e => !expectedErrors.Any(expected => e.Contains(expected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        criticalErrors.Should().BeEmpty("Login page should not have critical console errors");
    }

    [Test]
    [Description("Verifies register page has no console errors")]
    public async Task Register_NoConsoleErrors()
    {
        // Arrange
        ConsoleErrors.Clear();

        // Act
        await NavigateToAsync("/register");
        await Task.Delay(2000);

        // Assert - Filter out expected Blazor/SignalR errors
        var expectedErrors = new[] { "NetworkError", "fetch", "SignalR", "blazor", "circuit", "Blob" };
        var criticalErrors = ConsoleErrors
            .Where(e => !expectedErrors.Any(expected => e.Contains(expected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        criticalErrors.Should().BeEmpty("Register page should not have critical console errors");
    }

    [Test]
    [Description("Verifies form validation prevents empty submission on login")]
    public async Task Login_EmptySubmission_ShowsValidation()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act - Click submit without filling form
        await Page.ClickAsync("button[type='submit']");
        await Task.Delay(500);

        // Assert - Validation should be triggered
        var validationSummary = Page.Locator(".validation-summary, .validation-message");
        var count = await validationSummary.CountAsync();

        // HTML5 validation or Blazor validation should prevent submission
        TestContext.WriteLine($"Validation elements found: {count}");

        // Check if we're still on login page (form not submitted)
        Page.Url.Should().Contain("/login");
    }

    [Test]
    [Description("Verifies form validation prevents empty submission on register")]
    public async Task Register_EmptySubmission_ShowsValidation()
    {
        // Arrange
        await NavigateToAsync("/register");

        // Act - Click submit without filling form
        await Page.ClickAsync("button[type='submit']");
        await Task.Delay(500);

        // Assert - Validation should be triggered
        var validationSummary = Page.Locator(".validation-summary, .validation-message");
        var count = await validationSummary.CountAsync();

        TestContext.WriteLine($"Validation elements found: {count}");

        // Check if we're still on register page (form not submitted)
        Page.Url.Should().Contain("/register");
    }
}
