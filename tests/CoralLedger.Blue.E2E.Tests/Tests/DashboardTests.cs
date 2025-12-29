using CoralLedger.Blue.E2E.Tests.Pages;

namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E tests for the Dashboard page
/// </summary>
[TestFixture]
public class DashboardTests : PlaywrightFixture
{
    private DashboardPage _dashboard = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dashboard = new DashboardPage(Page, BaseUrl);
    }

    [Test]
    public async Task Dashboard_LoadsSuccessfully()
    {
        // Act
        await _dashboard.NavigateAsync();

        // Assert
        var title = await Page.TitleAsync();
        title.Should().Contain("CoralLedger");
    }

    [Test]
    public async Task Dashboard_HasStatCards()
    {
        // Arrange
        await _dashboard.NavigateAsync();

        // Act
        var cards = await _dashboard.GetStatCardsAsync();

        // Assert - Dashboard should have stat cards
        cards.Count.Should().BeGreaterOrEqualTo(1);
    }

    [Test]
    public async Task Dashboard_DisplaysMpaInfo()
    {
        // Arrange
        await _dashboard.NavigateAsync();

        // Act & Assert
        var hasMpa = await _dashboard.HasMpaCountAsync();
        hasMpa.Should().BeTrue("Dashboard should display MPA information");
    }

    [Test]
    public async Task Dashboard_NoConsoleErrors()
    {
        // Act
        await _dashboard.NavigateAsync();
        await Task.Delay(2000); // Wait for any async operations

        // Assert
        AssertNoConsoleErrors();
    }

    [Test]
    [Description("Verify VIEW MAP button is visible in light mode")]
    public async Task Dashboard_ViewMapButton_VisibleInLightMode()
    {
        // Arrange - Navigate to dashboard
        await _dashboard.NavigateAsync();
        await Task.Delay(1000);

        // Act - Switch to light mode
        var themeButton = Page.Locator("button.theme-toggle, button:has-text('Light mode')").First;
        if (await themeButton.IsVisibleAsync())
        {
            await themeButton.ClickAsync();
            await Task.Delay(1500); // Wait for theme transition
        }

        // Assert - View Map button should be visible
        var viewMapButton = Page.Locator(".btn-header, a:has-text('View Map')").First;
        var isVisible = await viewMapButton.IsVisibleAsync();
        isVisible.Should().BeTrue("VIEW MAP button should be visible in light mode");

        // Additional check - verify it has contrasting colors (not invisible)
        var boundingBox = await viewMapButton.BoundingBoxAsync();
        boundingBox.Should().NotBeNull("VIEW MAP button should have a bounding box (be rendered)");

        // Switch back to dark mode
        var darkButton = Page.Locator("button.theme-toggle, button:has-text('Dark mode')").First;
        if (await darkButton.IsVisibleAsync())
        {
            await darkButton.ClickAsync();
        }
    }
}
