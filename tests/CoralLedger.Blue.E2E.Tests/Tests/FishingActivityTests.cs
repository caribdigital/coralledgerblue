namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E tests for the Fishing Activity feature on the Map page.
/// Verifies the toggle button works and the API is called correctly.
/// </summary>
[TestFixture]
public class FishingActivityTests : PlaywrightFixture
{
    [Test]
    [Description("Verifies the Fishing Activity toggle is visible on the map page")]
    public async Task FishingActivity_ToggleIsVisible()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Find the fishing toggle
        var fishingToggle = Page.Locator("#fishingToggle, input[type='checkbox']:near(:text('Fishing Activity'))").First;

        // Assert
        var isVisible = await fishingToggle.IsVisibleAsync();
        isVisible.Should().BeTrue("Fishing Activity toggle should be visible on the map page");
    }

    [Test]
    [Description("Verifies the Fishing Activity toggle can be clicked")]
    public async Task FishingActivity_ToggleCanBeClicked()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Find and click the fishing toggle
        var fishingToggle = Page.Locator("#fishingToggle").First;
        var initialState = await fishingToggle.IsCheckedAsync();

        await fishingToggle.ClickAsync();
        await Task.Delay(500);

        var newState = await fishingToggle.IsCheckedAsync();

        // Assert
        newState.Should().NotBe(initialState, "Toggle state should change when clicked");
    }

    [Test]
    [Description("Verifies day filter buttons appear when Fishing Activity is enabled")]
    public async Task FishingActivity_DayFilterButtonsAppearWhenEnabled()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Enable fishing activity
        var fishingToggle = Page.Locator("#fishingToggle").First;
        if (!await fishingToggle.IsCheckedAsync())
        {
            await fishingToggle.ClickAsync();
            await Task.Delay(1000);
        }

        // Assert - Day filter buttons should appear
        var dayButtons = Page.Locator(".btn-group-sm button, .fishing-controls button");
        var buttonCount = await dayButtons.CountAsync();

        buttonCount.Should().BeGreaterOrEqualTo(3, "Should show 7d, 14d, 30d filter buttons when fishing activity is enabled");

        // Check for specific day buttons
        var sevenDayButton = Page.Locator("button:has-text('7d')");
        var fourteenDayButton = Page.Locator("button:has-text('14d')");
        var thirtyDayButton = Page.Locator("button:has-text('30d')");

        (await sevenDayButton.IsVisibleAsync()).Should().BeTrue("7d button should be visible");
        (await fourteenDayButton.IsVisibleAsync()).Should().BeTrue("14d button should be visible");
        (await thirtyDayButton.IsVisibleAsync()).Should().BeTrue("30d button should be visible");
    }

    [Test]
    [Description("Verifies the fishing events API is called when toggle is enabled")]
    public async Task FishingActivity_ApiIsCalledWhenEnabled()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Set up request interception to monitor API calls
        var apiCalled = false;
        await Page.RouteAsync("**/api/vessels/fishing-events/bahamas**", async route =>
        {
            apiCalled = true;
            await route.ContinueAsync();
        });

        // Act - Enable fishing activity
        var fishingToggle = Page.Locator("#fishingToggle").First;
        if (!await fishingToggle.IsCheckedAsync())
        {
            await fishingToggle.ClickAsync();
            await Task.Delay(3000); // Wait for API call
        }

        // Assert
        apiCalled.Should().BeTrue("Fishing events API should be called when toggle is enabled");
    }

    [Test]
    [Description("Verifies no console errors when toggling fishing activity")]
    public async Task FishingActivity_NoConsoleErrorsOnToggle()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);
        ConsoleErrors.Clear();

        // Act - Toggle fishing activity on and off
        var fishingToggle = Page.Locator("#fishingToggle").First;

        await fishingToggle.ClickAsync();
        await Task.Delay(2000);

        await fishingToggle.ClickAsync();
        await Task.Delay(1000);

        // Assert - Filter out expected "no data" messages
        var criticalErrors = ConsoleErrors
            .Where(e => !e.Contains("no data", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.Contains("No fishing events", StringComparison.OrdinalIgnoreCase))
            .ToList();

        criticalErrors.Should().BeEmpty("No console errors should occur when toggling fishing activity");
    }
}

/// <summary>
/// Visual fidelity tests for the Fishing Activity UI components.
/// </summary>
[TestFixture]
public class FishingActivityVisualTests : PlaywrightFixture
{
    [Test]
    [Description("Verifies the Fishing Activity toggle has proper visual styling")]
    public async Task Visual_FishingActivityToggleIsStyled()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Find the fishing controls container
        var fishingControls = Page.Locator(".fishing-controls").First;

        var isVisible = await fishingControls.IsVisibleAsync();
        isVisible.Should().BeTrue("Fishing controls should be visible");

        // Check toggle label is visible with white text on gradient header
        var label = Page.Locator("label[for='fishingToggle']").First;
        if (await label.IsVisibleAsync())
        {
            var color = await label.EvaluateAsync<string>("el => window.getComputedStyle(el).color");
            // Should be white or light colored text
            var colorMatch = System.Text.RegularExpressions.Regex.Match(color, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
            if (colorMatch.Success)
            {
                var r = int.Parse(colorMatch.Groups[1].Value);
                var g = int.Parse(colorMatch.Groups[2].Value);
                var b = int.Parse(colorMatch.Groups[3].Value);
                var isLight = r > 200 && g > 200 && b > 200;
                isLight.Should().BeTrue($"Label text should be light/white for visibility. Got: {color}");
            }
        }

        // Take screenshot for visual verification
        var screenshotPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "playwright-artifacts",
            "fishing-activity-toggle.png");

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        await fishingControls.ScreenshotAsync(new() { Path = screenshotPath });
        TestContext.AddTestAttachment(screenshotPath, "Fishing Activity Toggle");
    }

    [Test]
    [Description("Verifies day filter buttons have proper styling when fishing is enabled")]
    public async Task Visual_DayFilterButtonsAreStyled()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Enable fishing activity
        var fishingToggle = Page.Locator("#fishingToggle").First;
        if (!await fishingToggle.IsCheckedAsync())
        {
            await fishingToggle.ClickAsync();
            await Task.Delay(1000);
        }

        // Act - Check button styling
        var thirtyDayButton = Page.Locator("button:has-text('30d')").First;
        if (await thirtyDayButton.IsVisibleAsync())
        {
            // The 30d button should be selected by default (btn-light class)
            var classes = await thirtyDayButton.GetAttributeAsync("class") ?? "";
            var isSelected = classes.Contains("btn-light") || classes.Contains("active");

            // Take screenshot
            var screenshotPath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "playwright-artifacts",
                "fishing-day-buttons.png");

            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
            var buttonGroup = Page.Locator(".btn-group-sm").First;
            if (await buttonGroup.IsVisibleAsync())
            {
                await buttonGroup.ScreenshotAsync(new() { Path = screenshotPath });
                TestContext.AddTestAttachment(screenshotPath, "Fishing Day Filter Buttons");
            }

            isSelected.Should().BeTrue("30d button should be selected by default");
        }
    }

    [Test]
    [Description("Verifies fishing events badge appears or shows no-data message")]
    public async Task Visual_FishingEventsBadgeOrNoDataMessage()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Enable fishing activity
        var fishingToggle = Page.Locator("#fishingToggle").First;
        if (!await fishingToggle.IsCheckedAsync())
        {
            await fishingToggle.ClickAsync();
            await Task.Delay(3000); // Wait for API response
        }

        // Act - Check for either fishing events badge or no-data message
        var fishingBadge = Page.Locator(".fishing-events-badge").First;
        var isVisible = await fishingBadge.IsVisibleAsync();

        // Assert
        isVisible.Should().BeTrue("Should show either fishing events count or 'no data' badge");

        // Take screenshot
        var screenshotPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "playwright-artifacts",
            "fishing-events-badge.png");

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        await fishingBadge.ScreenshotAsync(new() { Path = screenshotPath });
        TestContext.AddTestAttachment(screenshotPath, "Fishing Events Badge");
    }
}
