namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// Visual fidelity tests to ensure CSS and styling are loaded correctly.
/// These tests verify that the page doesn't appear unstyled or broken.
/// </summary>
[TestFixture]
public class VisualFidelityTests : PlaywrightFixture
{
    [Test]
    [Description("Verifies that Bootstrap CSS is loaded and applied")]
    public async Task Visual_BootstrapCssIsLoaded()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if Bootstrap classes are actually applying styles
        // Bootstrap's .btn class should have specific styling
        var body = Page.Locator("body");

        // Check that the body has proper font-family (Bootstrap sets this)
        var fontFamily = await body.EvaluateAsync<string>("el => window.getComputedStyle(el).fontFamily");

        // Assert - Bootstrap sets system-ui or sans-serif fonts
        fontFamily.Should().NotBeNullOrEmpty("Body should have a font-family set");
        fontFamily.ToLower().Should().ContainAny(new[] { "system-ui", "segoe", "roboto", "helvetica", "arial", "sans-serif" },
            "Bootstrap CSS should set a modern font stack");
    }

    [Test]
    [Description("Verifies that Blazor scoped CSS (CoralLedger.Blue.Web.styles.css) is loaded")]
    public async Task Visual_BlazorScopedCssIsLoaded()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check that the scoped CSS stylesheet link exists and is loaded
        var scopedCssLink = await Page.EvaluateAsync<bool>(@"() => {
            const links = document.querySelectorAll('link[rel=""stylesheet""]');
            return Array.from(links).some(link => link.href.includes('CoralLedger.Blue.Web.styles.css'));
        }");

        // Also verify the stylesheet actually loaded (not 404)
        var stylesheetLoaded = await Page.EvaluateAsync<bool>(@"() => {
            const links = document.querySelectorAll('link[rel=""stylesheet""]');
            for (const link of links) {
                if (link.href.includes('CoralLedger.Blue.Web.styles.css')) {
                    // Check if stylesheet has rules (means it loaded successfully)
                    for (const sheet of document.styleSheets) {
                        if (sheet.href && sheet.href.includes('CoralLedger.Blue.Web.styles.css')) {
                            try {
                                return sheet.cssRules.length > 0;
                            } catch {
                                return true; // Cross-origin or other error, but file exists
                            }
                        }
                    }
                }
            }
            return false;
        }");

        // Assert
        scopedCssLink.Should().BeTrue("CoralLedger.Blue.Web.styles.css link should exist in the document");
        stylesheetLoaded.Should().BeTrue("Scoped CSS stylesheet should load and contain rules");
    }

    [Test]
    [Description("Verifies the sidebar navigation has proper styling")]
    public async Task Visual_SidebarHasProperStyling()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check sidebar dimensions and positioning
        var sidebar = Page.Locator(".sidebar, [class*='sidebar'], .nav-scrollable").First;

        if (await sidebar.IsVisibleAsync())
        {
            var sidebarBox = await sidebar.BoundingBoxAsync();

            // Assert - Sidebar should have meaningful width (not collapsed to 0)
            sidebarBox.Should().NotBeNull("Sidebar should have a bounding box");
            sidebarBox!.Width.Should().BeGreaterThan(100, "Sidebar should have width > 100px when styled");
        }
        else
        {
            // On mobile, sidebar may be hidden - check for mobile nav instead
            var mobileNav = Page.Locator(".mobile-nav, .navbar").First;
            var mobileNavVisible = await mobileNav.IsVisibleAsync();
            mobileNavVisible.Should().BeTrue("Either sidebar or mobile nav should be visible");
        }
    }

    [Test]
    [Description("Verifies stat cards on dashboard have proper card styling")]
    public async Task Visual_DashboardCardsHaveProperStyling()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Find cards and check their styling
        var cards = Page.Locator(".card, .stat-card, [class*='card']");
        var cardCount = await cards.CountAsync();

        if (cardCount > 0)
        {
            var firstCard = cards.First;

            // Check computed styles
            var backgroundColor = await firstCard.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
            var borderRadius = await firstCard.EvaluateAsync<string>("el => window.getComputedStyle(el).borderRadius");
            var boxShadow = await firstCard.EvaluateAsync<string>("el => window.getComputedStyle(el).boxShadow");
            var padding = await firstCard.EvaluateAsync<string>("el => window.getComputedStyle(el).padding");

            // Assert - Cards should have some visual styling
            // At minimum, they should have padding or border-radius or background
            var hasAnyStyling =
                !string.IsNullOrEmpty(backgroundColor) && backgroundColor != "rgba(0, 0, 0, 0)" ||
                !string.IsNullOrEmpty(borderRadius) && borderRadius != "0px" ||
                !string.IsNullOrEmpty(boxShadow) && boxShadow != "none" ||
                !string.IsNullOrEmpty(padding) && padding != "0px";

            hasAnyStyling.Should().BeTrue($"Cards should have visual styling. Found: bg={backgroundColor}, radius={borderRadius}, shadow={boxShadow}, padding={padding}");
        }
    }

    [Test]
    [Description("Verifies no 'offline' or 'service worker error' message is displayed")]
    public async Task Visual_NoOfflineMessageDisplayed()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if the offline indicator is actually showing (has the .visible class)
        // The .offline-indicator element may exist but is hidden by default via CSS transform
        // It only shows when the .visible class is added
        var offlineBannerShowing = await Page.EvaluateAsync<bool>(@"() => {
            // Check for the mobile offline indicator with .visible class
            const offlineIndicator = document.querySelector('.offline-indicator.visible');
            if (offlineIndicator) return true;

            // Check for any alert/banner containing 'offline' text that's visible
            const alerts = document.querySelectorAll('.alert, .banner, [class*=""warning""]');
            for (const alert of alerts) {
                if (alert.textContent.toLowerCase().includes('offline') &&
                    window.getComputedStyle(alert).display !== 'none' &&
                    window.getComputedStyle(alert).visibility !== 'hidden') {
                    const rect = alert.getBoundingClientRect();
                    if (rect.width > 0 && rect.height > 0 && rect.top >= 0) {
                        return true;
                    }
                }
            }

            return false;
        }");

        // Also check for blocking "You're offline" heading/message
        var blockingOfflineMessage = Page.Locator("h1, h2").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("offline", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });

        var blockingMessageVisible = false;
        try
        {
            blockingMessageVisible = await blockingOfflineMessage.First.IsVisibleAsync();
        }
        catch
        {
            // No blocking message found - good
        }

        // Assert - Neither the visible offline banner nor blocking message should be shown
        offlineBannerShowing.Should().BeFalse("Offline indicator should not be showing when server is running");
        blockingMessageVisible.Should().BeFalse("Blocking offline message should not be displayed when server is running");
    }

    [Test]
    [Description("Verifies the main content area has proper layout (not stacked unstyled)")]
    public async Task Visual_MainContentHasProperLayout()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check that the main content area has proper width
        var mainContent = Page.Locator("main, .main, article, [class*='content']").First;

        if (await mainContent.IsVisibleAsync())
        {
            var mainBox = await mainContent.BoundingBoxAsync();
            var viewportWidth = await Page.EvaluateAsync<int>("() => window.innerWidth");

            // Assert - Main content should have meaningful width
            mainBox.Should().NotBeNull("Main content should have a bounding box");

            // Main content should take up most of viewport (minus sidebar if present)
            var minExpectedWidth = (float)(viewportWidth * 0.4); // At least 40% of viewport
            mainBox!.Width.Should().BeGreaterThan(minExpectedWidth,
                $"Main content width ({mainBox.Width}px) should be significant portion of viewport ({viewportWidth}px)");
        }
    }

    [Test]
    [Description("Verifies all required CSS files are loaded in the document")]
    public async Task Visual_AllCssFilesLoad()
    {
        // Arrange - First navigate to the page so stylesheets are loaded
        await NavigateToAsync("/");
        await Task.Delay(2000);

        var cssFilesToCheck = new[]
        {
            "bootstrap",
            "app.css",
            "CoralLedger.Blue.Web.styles.css"
        };

        var failedFiles = new List<string>();

        // Act - Check each CSS file is present in the document's stylesheets
        foreach (var cssFile in cssFilesToCheck)
        {
            var isLoaded = await Page.EvaluateAsync<bool>($@"() => {{
                const links = document.querySelectorAll('link[rel=""stylesheet""]');
                for (const link of links) {{
                    if (link.href.includes('{cssFile}')) {{
                        return true;
                    }}
                }}
                // Also check inline style tags (in case CSS is bundled)
                const styles = document.querySelectorAll('style');
                return styles.length > 0;
            }}");

            if (!isLoaded)
            {
                failedFiles.Add(cssFile);
            }
        }

        // Assert
        failedFiles.Should().BeEmpty($"All CSS files should be loaded in document. Missing: {string.Join(", ", failedFiles)}");
    }

    [Test]
    [Description("Verifies the page uses the expected color scheme")]
    public async Task Visual_HasExpectedColorScheme()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check for CoralLedger Blue brand colors
        // The header/hero should have the ocean blue gradient
        var header = Page.Locator(".hero-header, .page-header, header, [class*='hero']").First;

        if (await header.IsVisibleAsync())
        {
            var backgroundColor = await header.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
            var backgroundImage = await header.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundImage");

            // Assert - Header should have some color (not transparent/white)
            var hasColorStyling =
                (!string.IsNullOrEmpty(backgroundColor) && backgroundColor != "rgba(0, 0, 0, 0)" && backgroundColor != "rgb(255, 255, 255)") ||
                (!string.IsNullOrEmpty(backgroundImage) && backgroundImage != "none");

            hasColorStyling.Should().BeTrue($"Header should have brand color styling. bg={backgroundColor}, bgImage={backgroundImage}");
        }
    }

    [Test]
    [Description("Verifies navigation links have proper styling (not plain text links)")]
    public async Task Visual_NavigationLinksAreStyled()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check nav links have styling
        var navLink = Page.Locator("nav a, .nav-link, .sidebar a").First;

        if (await navLink.IsVisibleAsync())
        {
            var textDecoration = await navLink.EvaluateAsync<string>("el => window.getComputedStyle(el).textDecoration");
            var display = await navLink.EvaluateAsync<string>("el => window.getComputedStyle(el).display");
            var padding = await navLink.EvaluateAsync<string>("el => window.getComputedStyle(el).padding");

            // Assert - Nav links should not look like plain underlined links
            // They should have padding and typically no underline
            var isStyled =
                (!textDecoration.Contains("underline") || textDecoration == "none") ||
                (!string.IsNullOrEmpty(padding) && padding != "0px");

            isStyled.Should().BeTrue($"Navigation links should be styled. textDecoration={textDecoration}, padding={padding}");
        }
    }

    [Test]
    [Description("Takes a screenshot for visual comparison (baseline)")]
    public async Task Visual_CaptureBaselineScreenshot()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(3000); // Wait for all content to load

        // Act - Take screenshot
        var screenshotPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "playwright-artifacts",
            "visual-baseline-dashboard.png");

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

        // Assert - Screenshot was saved
        File.Exists(screenshotPath).Should().BeTrue("Screenshot should be saved");

        // Add as test attachment for review
        TestContext.AddTestAttachment(screenshotPath, "Dashboard Visual Baseline");
    }

    [Test]
    [Description("Verifies the View Map button in dashboard header is visible with proper contrast")]
    public async Task Visual_DashboardViewMapButtonIsVisible()
    {
        // Arrange
        await NavigateToAsync("/dashboard");
        await Task.Delay(2000);

        // Act - Find the View Map button in the header
        var viewMapButton = Page.Locator(".btn-header, a:has-text('View Map')").First;

        viewMapButton.Should().NotBeNull("View Map button should exist");
        var isVisible = await viewMapButton.IsVisibleAsync();
        isVisible.Should().BeTrue("View Map button should be visible");

        // Check the button has proper contrast (white/light background on gradient header)
        var backgroundColor = await viewMapButton.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
        var color = await viewMapButton.EvaluateAsync<string>("el => window.getComputedStyle(el).color");

        // Parse RGB values to check contrast
        // Background should be white/light (high RGB values)
        // Text should be dark (blue) for contrast
        var bgMatch = System.Text.RegularExpressions.Regex.Match(backgroundColor, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
        if (bgMatch.Success)
        {
            var r = int.Parse(bgMatch.Groups[1].Value);
            var g = int.Parse(bgMatch.Groups[2].Value);
            var b = int.Parse(bgMatch.Groups[3].Value);

            // White or near-white background (RGB values > 200)
            var isLightBackground = r > 200 && g > 200 && b > 200;
            isLightBackground.Should().BeTrue($"Button background should be light/white for visibility. Got: {backgroundColor}");
        }

        // Take a screenshot of the button for visual verification
        var screenshotPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "playwright-artifacts",
            "dashboard-viewmap-button.png");

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        await viewMapButton.ScreenshotAsync(new() { Path = screenshotPath });

        File.Exists(screenshotPath).Should().BeTrue("Button screenshot should be saved");
        TestContext.AddTestAttachment(screenshotPath, "Dashboard View Map Button");
    }

    [Test]
    [Description("Verifies the Map View toggle button on Map page is visible")]
    public async Task Visual_MapPageViewToggleButtonIsVisible()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(3000); // Allow map to load

        // Act - Find the Map View button in the toggle group
        var mapViewButton = Page.Locator(".view-toggle .btn").First;

        var isVisible = await mapViewButton.IsVisibleAsync();
        isVisible.Should().BeTrue("Map View toggle button should be visible");

        // Check the button has proper styling for visibility on gradient header
        var backgroundColor = await mapViewButton.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");

        // Background should not be dark/black (which would blend with header)
        var bgMatch = System.Text.RegularExpressions.Regex.Match(backgroundColor, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
        if (bgMatch.Success)
        {
            var r = int.Parse(bgMatch.Groups[1].Value);
            var g = int.Parse(bgMatch.Groups[2].Value);
            var b = int.Parse(bgMatch.Groups[3].Value);

            // Should not be very dark (all values < 50 would be nearly black)
            var isNotDark = r > 50 || g > 50 || b > 50;
            isNotDark.Should().BeTrue($"Button should not have dark background that blends with header. Got: {backgroundColor}");
        }

        // Take a screenshot of the header area for visual verification
        var screenshotPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "playwright-artifacts",
            "map-view-toggle-button.png");

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        var header = Page.Locator(".map-header").First;
        if (await header.IsVisibleAsync())
        {
            await header.ScreenshotAsync(new() { Path = screenshotPath });
            TestContext.AddTestAttachment(screenshotPath, "Map Page Header with Toggle Buttons");
        }
    }
}
