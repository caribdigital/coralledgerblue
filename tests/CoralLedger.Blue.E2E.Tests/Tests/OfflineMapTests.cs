using CoralLedger.Blue.E2E.Tests.Pages;
using Microsoft.Playwright;

namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E tests for offline map tile caching functionality.
/// Tests verify that tiles can be downloaded, cached, and managed correctly.
/// </summary>
[TestFixture]
public class OfflineMapTests : PlaywrightFixture
{
    [Test]
    [Description("Verifies that the OfflineMapManager component is visible on the map page")]
    public async Task OfflineMapManager_IsVisible()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000); // Wait for map to load

        // Assert - Check if offline map manager is present
        var offlineMapSection = Page.Locator(".offline-map-manager");
        await Expect(offlineMapSection).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verifies that cache statistics are displayed")]
    public async Task OfflineMapManager_DisplaysCacheStats()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check for cache statistics elements
        var totalTilesLabel = Page.GetByText("Total Tiles");
        var storageLabel = Page.GetByText("Storage Used");

        await Expect(totalTilesLabel).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(storageLabel).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verifies that the estimate button is clickable and triggers estimation")]
    public async Task OfflineMapManager_EstimateButton_Works()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Click the estimate button
        var estimateButton = Page.GetByRole(AriaRole.Button, new() { NameString = "Estimate Current View" });
        await estimateButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await estimateButton.ClickAsync();

        // Wait for estimation to complete
        await Task.Delay(2000);

        // Assert - Check if estimate result is displayed
        var estimateAlert = Page.Locator(".alert-info").Filter(new() { HasTextString = "Estimated:" });
        await Expect(estimateAlert).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    [Description("Verifies that theme selection dropdown is available")]
    public async Task OfflineMapManager_ThemeSelector_IsAvailable()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check for theme selector
        var themeLabel = Page.GetByText("Theme", new() { Exact = false });
        var themeSelect = Page.Locator("select").Filter(new() { HasTextString = "Dark" });

        await Expect(themeLabel).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(themeSelect).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verifies that zoom level inputs are available and have valid ranges")]
    public async Task OfflineMapManager_ZoomInputs_AreValid()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check for zoom inputs
        var minZoomLabel = Page.GetByText("Min Zoom");
        var maxZoomLabel = Page.GetByText("Max Zoom");

        await Expect(minZoomLabel).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(maxZoomLabel).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Verify zoom inputs have correct attributes
        var minZoomInput = Page.Locator("input[type='number']").Filter(new() { HasTextString = "" }).First;
        var minValue = await minZoomInput.GetAttributeAsync("min");
        var maxValue = await minZoomInput.GetAttributeAsync("max");

        minValue.Should().Be("0");
        maxValue.Should().Be("19");
    }

    [Test]
    [Description("Verifies that download button is initially disabled until estimate is done")]
    public async Task OfflineMapManager_DownloadButton_InitiallyDisabled()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Download button should be disabled initially
        var downloadButton = Page.GetByRole(AriaRole.Button, new() { NameString = "Download Current View" });
        await Expect(downloadButton).ToBeVisibleAsync(new() { Timeout = 10000 });
        
        var isDisabled = await downloadButton.IsDisabledAsync();
        isDisabled.Should().BeTrue("Download button should be disabled until estimate is performed");
    }

    [Test]
    [Description("Verifies that clear cache button is available")]
    public async Task OfflineMapManager_ClearCacheButton_IsAvailable()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check for clear cache button
        var clearButton = Page.GetByRole(AriaRole.Button, new() { NameString = "Clear All Cache" });
        await Expect(clearButton).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verifies that clear old tiles button is available")]
    public async Task OfflineMapManager_ClearOldTilesButton_IsAvailable()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check for clear old tiles button
        var clearOldButton = Page.GetByRole(AriaRole.Button, new() { NameString = "Clear Tiles Older Than 30 Days" });
        await Expect(clearOldButton).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verifies that refresh button is available")]
    public async Task OfflineMapManager_RefreshButton_IsAvailable()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check for refresh button
        var refreshButton = Page.Locator("button").Filter(new() { HasTextString = "Refresh" });
        await Expect(refreshButton).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verifies JavaScript tile-cache object is initialized")]
    public async Task TileCache_IsInitialized()
    {
        // Arrange & Act
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Assert - Check if tileCache object exists in window
        var tileCacheExists = await Page.EvaluateAsync<bool>("typeof window.tileCache !== 'undefined'");
        tileCacheExists.Should().BeTrue("window.tileCache object should be initialized");
    }

    [Test]
    [Description("Verifies getTileKey function generates correct keys")]
    public async Task TileCache_GetTileKey_GeneratesCorrectKeys()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Call getTileKey function
        var key1 = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('dark', 10, 5, 7)");
        var key2 = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('light', 12, 100, 200)");

        // Assert
        key1.Should().Be("dark_10_5_7");
        key2.Should().Be("light_12_100_200");
    }

    [Test]
    [Description("Verifies latLngToTile function converts coordinates correctly")]
    public async Task TileCache_LatLngToTile_ConvertsCorrectly()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Test conversion for a known location (Nassau, Bahamas: 25.0443, -77.3504)
        var tile = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(25.0443, -77.3504, 10)"
        );

        // Assert - Verify tile coordinates are reasonable
        tile.Should().ContainKey("x");
        tile.Should().ContainKey("y");
        
        var x = Convert.ToInt32(tile["x"]);
        var y = Convert.ToInt32(tile["y"]);
        
        x.Should().BeGreaterThan(0).And.BeLessThan(1024); // At zoom 10, tiles range from 0 to 1023
        y.Should().BeGreaterThan(0).And.BeLessThan(1024);
    }

    [Test]
    [Description("Verifies buildTileUrl function correctly replaces placeholders")]
    public async Task TileCache_BuildTileUrl_ReplacesPlaceholders()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act
        var url = await Page.EvaluateAsync<string>(
            "window.tileCache.buildTileUrl('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', 10, 5, 7)"
        );

        // Assert
        url.Should().Be("https://a.tile.openstreetmap.org/10/5/7.png");
    }

    [Test]
    [Description("Verifies getTilesForRegion calculates correct tile count")]
    public async Task TileCache_GetTilesForRegion_CalculatesCorrectly()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act - Calculate tiles for a small region at zoom 10
        var tiles = await Page.EvaluateAsync<object[]>(
            @"window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10
            )"
        );

        // Assert - Should have a reasonable number of tiles
        tiles.Should().NotBeNull();
        tiles.Length.Should().BeGreaterThan(0).And.BeLessThan(100);
    }

    [Test]
    [Description("Verifies estimateRegionSize returns correct structure")]
    public async Task TileCache_EstimateRegionSize_ReturnsCorrectStructure()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act
        var estimate = await Page.EvaluateAsync<Dictionary<string, object>>(
            @"window.tileCache.estimateRegionSize(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10
            )"
        );

        // Assert
        estimate.Should().ContainKey("tileCount");
        estimate.Should().ContainKey("estimatedBytes");
        estimate.Should().ContainKey("estimatedMB");
        
        var tileCount = Convert.ToInt32(estimate["tileCount"]);
        tileCount.Should().BeGreaterThan(0);
    }

    [Test]
    [Description("Verifies cache statistics can be retrieved")]
    public async Task TileCache_GetStats_ReturnsStatistics()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act
        var stats = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.getStats()"
        );

        // Assert
        stats.Should().ContainKey("totalTiles");
        stats.Should().ContainKey("totalBytes");
        stats.Should().ContainKey("totalMB");
    }

    [Test]
    [Description("Verifies isOnline function returns correct status")]
    public async Task TileCache_IsOnline_ReturnsCorrectStatus()
    {
        // Arrange
        await NavigateToAsync("/map");
        await Task.Delay(2000);

        // Act
        var isOnline = await Page.EvaluateAsync<bool>("window.tileCache.isOnline()");

        // Assert - Should be online in test environment
        isOnline.Should().BeTrue("Browser should be online during tests");
    }
}
