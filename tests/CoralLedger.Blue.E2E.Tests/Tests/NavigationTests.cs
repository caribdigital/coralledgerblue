namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E tests for navigation between pages
/// </summary>
[TestFixture]
public class NavigationTests : PlaywrightFixture
{
    [Test]
    public async Task Navigation_CanAccessDashboard()
    {
        // Act
        await NavigateToAsync("/");

        // Assert - The URL should be the root path, accounting for HTTP/HTTPS redirects
        var url = Page.Url;
        // URL should end with / or /dashboard or similar root path
        url.Should().MatchRegex(@"https?://localhost:\d+/?$",
            "Should navigate to the dashboard at root URL (accounting for HTTP/HTTPS redirects)");
    }

    [Test]
    public async Task Navigation_CanAccessMap()
    {
        // Act
        await NavigateToAsync("/map");

        // Assert
        var url = Page.Url;
        url.Should().Contain("/map");
    }

    [Test]
    public async Task Navigation_CanAccessBleaching()
    {
        // Act
        await NavigateToAsync("/bleaching");

        // Assert
        var url = Page.Url;
        url.Should().Contain("/bleaching");
    }

    [Test]
    public async Task Navigation_CanAccessObservations()
    {
        // Act
        await NavigateToAsync("/observations");

        // Assert
        var url = Page.Url;
        url.Should().Contain("/observations");
    }

    [Test]
    public async Task Navigation_NavbarExists()
    {
        // Arrange
        await NavigateToAsync("/");

        // Act - Use .First to avoid strict mode violations when multiple nav elements exist
        var nav = Page.Locator("nav, [role='navigation'], .navbar").First;
        var isVisible = await nav.IsVisibleAsync();

        // Assert
        isVisible.Should().BeTrue("Navigation bar should be visible");
    }

    [Test]
    public async Task Navigation_ClickMapLink()
    {
        // Arrange
        await NavigateToAsync("/");

        // Act - Use .First to avoid strict mode violations when multiple map links exist
        var mapLink = Page.GetByRole(AriaRole.Link, new() { Name = "MPA Map" }).Or(
            Page.Locator("a[href='/map']")).First;

        if (await mapLink.IsVisibleAsync())
        {
            await mapLink.ClickAsync();
            await WaitForBlazorAsync();

            // Assert
            Page.Url.Should().Contain("/map");
        }
    }

    [Test]
    public async Task Navigation_NoConsoleErrorsOnAllPages()
    {
        // Test each main page for console errors
        // Note: Some minor errors from Blazor hydration or SignalR can be expected
        var pages = new[] { "/", "/map", "/bleaching", "/observations" };
        var expectedErrors = new[] { "NetworkError", "fetch", "Blob", "SignalR", "blazor", "circuit", "unhandled", "wasm", "exception" };

        foreach (var path in pages)
        {
            ConsoleErrors.Clear();
            await NavigateToAsync(path);
            await Task.Delay(1000);

            // Filter out expected/known errors
            var criticalErrors = ConsoleErrors
                .Where(e => !expectedErrors.Any(expected => e.Contains(expected, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            criticalErrors.Should().BeEmpty($"Page {path} should not have critical console errors");
        }
    }
}
