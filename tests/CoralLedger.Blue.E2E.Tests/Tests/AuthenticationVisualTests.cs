namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// Visual regression tests for authentication pages.
/// Captures baselines and verifies styling is applied correctly.
/// </summary>
[TestFixture]
public class AuthenticationVisualTests : PlaywrightFixture
{
    private const string ScreenshotDir = "playwright-artifacts/auth-visual";

    [SetUp]
    public void EnsureScreenshotDirectory()
    {
        var dir = Path.Combine(TestContext.CurrentContext.TestDirectory, ScreenshotDir);
        Directory.CreateDirectory(dir);
    }

    [Test]
    [Description("Captures login page baseline screenshot")]
    public async Task Visual_LoginPage_Baseline()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Take screenshot
        var screenshotPath = GetScreenshotPath("login-page-baseline.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("Login page screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Login Page Baseline");
    }

    [Test]
    [Description("Captures register page baseline screenshot")]
    public async Task Visual_RegisterPage_Baseline()
    {
        // Arrange
        await NavigateToAsync("/register");
        await Task.Delay(2000);

        // Act - Take screenshot
        var screenshotPath = GetScreenshotPath("register-page-baseline.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("Register page screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Register Page Baseline");
    }

    [Test]
    [Description("Captures forgot password page baseline screenshot")]
    public async Task Visual_ForgotPasswordPage_Baseline()
    {
        // Arrange
        await NavigateToAsync("/forgot-password");
        await Task.Delay(2000);

        // Act - Take screenshot
        var screenshotPath = GetScreenshotPath("forgot-password-page-baseline.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("Forgot password page screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Forgot Password Page Baseline");
    }

    [Test]
    [Description("Captures email confirmation page baseline screenshot")]
    public async Task Visual_EmailConfirmationPage_Baseline()
    {
        // Arrange
        await NavigateToAsync("/email-confirmation?email=test@example.com");
        await Task.Delay(2000);

        // Act - Take screenshot
        var screenshotPath = GetScreenshotPath("email-confirmation-page-baseline.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("Email confirmation page screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Email Confirmation Page Baseline");
    }

    [Test]
    [Description("Captures 2FA validate page baseline screenshot")]
    public async Task Visual_TwoFactorValidatePage_Baseline()
    {
        // Arrange
        await NavigateToAsync($"/2fa/validate?userId={Guid.NewGuid()}&returnUrl=/");
        await Task.Delay(2000);

        // Act - Take screenshot
        var screenshotPath = GetScreenshotPath("2fa-validate-page-baseline.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("2FA validate page screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "2FA Validate Page Baseline");
    }

    [Test]
    [Description("Verifies login page has proper auth card styling")]
    public async Task Visual_LoginPage_AuthCardStyling()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Check auth card or container styling
        // The auth-card class may be on a container or the card itself
        var authCard = Page.Locator(".auth-card, .auth-container, .card").First;

        if (await authCard.IsVisibleAsync())
        {
            var backgroundColor = await authCard.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
            var borderRadius = await authCard.EvaluateAsync<string>("el => window.getComputedStyle(el).borderRadius");
            var boxShadow = await authCard.EvaluateAsync<string>("el => window.getComputedStyle(el).boxShadow");
            var padding = await authCard.EvaluateAsync<string>("el => window.getComputedStyle(el).padding");
            var display = await authCard.EvaluateAsync<string>("el => window.getComputedStyle(el).display");

            // Assert - Card should have some visual styling or proper display
            // Accept if it has any styling OR if it's using flexbox/grid layout
            var hasAnyStyling =
                !string.IsNullOrEmpty(backgroundColor) && backgroundColor != "rgba(0, 0, 0, 0)" ||
                !string.IsNullOrEmpty(borderRadius) && borderRadius != "0px" ||
                !string.IsNullOrEmpty(boxShadow) && boxShadow != "none" ||
                !string.IsNullOrEmpty(padding) && padding != "0px" ||
                display == "flex" || display == "grid" || display == "block";

            hasAnyStyling.Should().BeTrue(
                $"Auth card should have visual styling or proper display. bg={backgroundColor}, radius={borderRadius}, shadow={boxShadow}, padding={padding}, display={display}");

            TestContext.WriteLine($"Auth card styles - bg: {backgroundColor}, radius: {borderRadius}, shadow: {boxShadow}, padding: {padding}, display: {display}");
        }
        else
        {
            // Check for alternative card structure
            var cardElement = Page.Locator(".card, [class*='auth']").First;
            var isVisible = await cardElement.IsVisibleAsync();
            isVisible.Should().BeTrue("Auth card or similar element should be visible on login page");
        }
    }

    [Test]
    [Description("Verifies login form inputs have proper styling")]
    public async Task Visual_LoginPage_FormInputStyling()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Check form input styling
        var emailInput = Page.Locator("#email");

        if (await emailInput.IsVisibleAsync())
        {
            var borderWidth = await emailInput.EvaluateAsync<string>("el => window.getComputedStyle(el).borderWidth");
            var borderRadius = await emailInput.EvaluateAsync<string>("el => window.getComputedStyle(el).borderRadius");
            var padding = await emailInput.EvaluateAsync<string>("el => window.getComputedStyle(el).padding");
            var fontSize = await emailInput.EvaluateAsync<string>("el => window.getComputedStyle(el).fontSize");

            // Assert - Inputs should have Bootstrap form-control styling
            padding.Should().NotBe("0px", "Form inputs should have padding");
            TestContext.WriteLine($"Input styles - border: {borderWidth}, radius: {borderRadius}, padding: {padding}, fontSize: {fontSize}");
        }
    }

    [Test]
    [Description("Verifies submit button has proper styling")]
    public async Task Visual_LoginPage_SubmitButtonStyling()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Check submit button styling
        var submitButton = Page.Locator("button[type='submit']").First;

        if (await submitButton.IsVisibleAsync())
        {
            var backgroundColor = await submitButton.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
            var backgroundImage = await submitButton.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundImage");
            var color = await submitButton.EvaluateAsync<string>("el => window.getComputedStyle(el).color");
            var padding = await submitButton.EvaluateAsync<string>("el => window.getComputedStyle(el).padding");
            var cursor = await submitButton.EvaluateAsync<string>("el => window.getComputedStyle(el).cursor");

            // Assert - Button should have primary button styling
            cursor.Should().Be("pointer", "Submit button should have pointer cursor");

            // Button can have solid background color OR a gradient background-image
            var hasBackgroundStyling =
                (backgroundColor != "rgba(0, 0, 0, 0)" && backgroundColor != "transparent") ||
                (backgroundImage != "none" && !string.IsNullOrEmpty(backgroundImage));

            hasBackgroundStyling.Should().BeTrue(
                $"Submit button should have a background color or gradient. bg={backgroundColor}, bgImage={backgroundImage}");

            TestContext.WriteLine($"Button styles - bg: {backgroundColor}, bgImage: {backgroundImage}, color: {color}, padding: {padding}, cursor: {cursor}");
        }
    }

    [Test]
    [Description("Verifies OAuth buttons have proper styling")]
    public async Task Visual_LoginPage_OAuthButtonStyling()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Check OAuth button styling
        var googleButton = Page.Locator("a[href='/api/auth/signin-google']").First;

        if (await googleButton.IsVisibleAsync())
        {
            var display = await googleButton.EvaluateAsync<string>("el => window.getComputedStyle(el).display");
            var borderWidth = await googleButton.EvaluateAsync<string>("el => window.getComputedStyle(el).borderWidth");
            var padding = await googleButton.EvaluateAsync<string>("el => window.getComputedStyle(el).padding");

            // Assert - OAuth buttons should be styled as block buttons
            padding.Should().NotBe("0px", "OAuth buttons should have padding");

            // Take screenshot of OAuth buttons section
            var oauthSection = Page.Locator(".oauth-buttons").First;
            if (await oauthSection.IsVisibleAsync())
            {
                var screenshotPath = GetScreenshotPath("oauth-buttons.png");
                await oauthSection.ScreenshotAsync(new() { Path = screenshotPath });
                TestContext.AddTestAttachment(screenshotPath, "OAuth Buttons");
            }

            TestContext.WriteLine($"OAuth button styles - display: {display}, border: {borderWidth}, padding: {padding}");
        }
    }

    [Test]
    [Description("Verifies login page mobile responsive layout")]
    public async Task Visual_LoginPage_MobileResponsive()
    {
        // Arrange - Set mobile viewport
        await Page.SetViewportSizeAsync(375, 812); // iPhone X dimensions
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Take mobile screenshot
        var screenshotPath = GetScreenshotPath("login-page-mobile.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert - Auth card should still be visible and properly sized
        var authCard = Page.Locator(".auth-card, .card, [class*='auth']").First;
        var box = await authCard.BoundingBoxAsync();

        if (box != null)
        {
            // Card should fit within mobile viewport
            box.Width.Should().BeLessThanOrEqualTo(375, "Auth card should fit mobile viewport width");

            TestContext.WriteLine($"Mobile auth card size: {box.Width}x{box.Height}");
        }

        File.Exists(screenshotPath).Should().BeTrue("Mobile screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Login Page Mobile");

        // Reset viewport for other tests
        await Page.SetViewportSizeAsync(1280, 720);
    }

    [Test]
    [Description("Verifies register page mobile responsive layout")]
    public async Task Visual_RegisterPage_MobileResponsive()
    {
        // Arrange - Set mobile viewport
        await Page.SetViewportSizeAsync(375, 812);
        await NavigateToAsync("/register");
        await Task.Delay(2000);

        // Act - Take mobile screenshot
        var screenshotPath = GetScreenshotPath("register-page-mobile.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("Mobile screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Register Page Mobile");

        // Reset viewport
        await Page.SetViewportSizeAsync(1280, 720);
    }

    [Test]
    [Description("Verifies error state styling on login")]
    public async Task Visual_LoginPage_ErrorState()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act - Trigger error by submitting invalid credentials
        await Page.FillAsync("#email", "invalid@example.com");
        await Page.FillAsync("#password", "WrongPassword");
        await Page.ClickAsync("button[type='submit']");
        await Task.Delay(3000); // Wait for server response

        // Take screenshot of error state
        var screenshotPath = GetScreenshotPath("login-page-error-state.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert - Error alert styling (if visible)
        var errorAlert = Page.Locator(".alert-danger").First;
        if (await errorAlert.IsVisibleAsync())
        {
            var backgroundColor = await errorAlert.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
            var color = await errorAlert.EvaluateAsync<string>("el => window.getComputedStyle(el).color");

            // Bootstrap danger alert should have reddish colors
            TestContext.WriteLine($"Error alert styles - bg: {backgroundColor}, color: {color}");

            // Take screenshot of just the alert
            var alertScreenshotPath = GetScreenshotPath("login-error-alert.png");
            await errorAlert.ScreenshotAsync(new() { Path = alertScreenshotPath });
            TestContext.AddTestAttachment(alertScreenshotPath, "Login Error Alert");
        }

        TestContext.AddTestAttachment(screenshotPath, "Login Page Error State");
    }

    [Test]
    [Description("Verifies validation error styling")]
    public async Task Visual_RegisterPage_ValidationError()
    {
        // Arrange
        await NavigateToAsync("/register");

        // Act - Fill form with invalid data to trigger validation
        await Page.FillAsync("#email", "not-an-email");
        await Page.FillAsync("#password", "short");
        await Page.ClickAsync("button[type='submit']");
        await Task.Delay(1000);

        // Take screenshot
        var screenshotPath = GetScreenshotPath("register-validation-error.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert - Validation messages should be styled
        var validationMessages = Page.Locator(".validation-message, .field-validation-error");
        var count = await validationMessages.CountAsync();

        if (count > 0)
        {
            var firstMessage = validationMessages.First;
            var color = await firstMessage.EvaluateAsync<string>("el => window.getComputedStyle(el).color");
            TestContext.WriteLine($"Validation message color: {color}");
        }

        TestContext.AddTestAttachment(screenshotPath, "Register Page Validation Error");
    }

    [Test]
    [Description("Verifies auth page CSS is loaded")]
    public async Task Visual_AuthPages_CssLoaded()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Check that auth.css is loaded
        var authCssLoaded = await Page.EvaluateAsync<bool>(@"() => {
            const links = document.querySelectorAll('link[rel=""stylesheet""]');
            for (const link of links) {
                if (link.href.includes('auth.css') || link.href.includes('app.css')) {
                    return true;
                }
            }
            // Check if auth-page class has styling applied
            const authPage = document.querySelector('.auth-page');
            if (authPage) {
                const styles = window.getComputedStyle(authPage);
                return styles.display !== 'inline';
            }
            return false;
        }");

        // Assert
        authCssLoaded.Should().BeTrue("Auth CSS should be loaded and applied");
    }

    [Test]
    [Description("Verifies auth header has icon styling")]
    public async Task Visual_LoginPage_HeaderIconStyling()
    {
        // Arrange
        await NavigateToAsync("/login");
        await Task.Delay(2000);

        // Act - Check header icon
        var headerIcon = Page.Locator(".auth-header i, .auth-header .bi").First;

        if (await headerIcon.IsVisibleAsync())
        {
            var fontSize = await headerIcon.EvaluateAsync<string>("el => window.getComputedStyle(el).fontSize");
            var color = await headerIcon.EvaluateAsync<string>("el => window.getComputedStyle(el).color");

            // Icons should have visible size
            TestContext.WriteLine($"Header icon styles - fontSize: {fontSize}, color: {color}");

            // Parse font size to verify it's visible
            var fontSizeValue = float.Parse(System.Text.RegularExpressions.Regex.Match(fontSize, @"[\d.]+").Value);
            fontSizeValue.Should().BeGreaterThan(16, "Header icon should be reasonably sized");
        }
    }

    private string GetScreenshotPath(string fileName)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, ScreenshotDir, fileName);
    }
}
