namespace CoralLedger.E2E.Tests.Pages;

/// <summary>
/// Page object for the Map page with Leaflet map component
/// </summary>
public class MapPage : BasePage
{
    public override string Path => "/map";

    public MapPage(IPage page, string baseUrl) : base(page, baseUrl)
    {
    }

    protected override async Task WaitForPageLoadAsync()
    {
        await base.WaitForPageLoadAsync();
        // Wait for Leaflet map container to render
        await Page.WaitForSelectorAsync(".leaflet-map-container, .leaflet-container, [class*='map']",
            new PageWaitForSelectorOptions { Timeout = 15000 });
        // Wait for loading overlay to disappear
        await Page.WaitForFunctionAsync(@"() => {
            const overlay = document.querySelector('.loading-overlay');
            return !overlay || overlay.style.display === 'none' || !document.body.contains(overlay);
        }", new PageWaitForFunctionOptions { Timeout = 20000 });
    }

    public async Task<bool> IsMapContainerVisibleAsync()
    {
        // Check for Leaflet container class
        var mapContainer = Page.Locator(".leaflet-map-container, .leaflet-container").First;
        return await mapContainer.IsVisibleAsync();
    }

    public async Task<bool> IsLeafletMapRenderedAsync()
    {
        // Leaflet uses SVG elements for vector layers (polygons, etc.)
        var leafletPane = Page.Locator(".leaflet-pane, .leaflet-tile-container").First;
        return await leafletPane.IsVisibleAsync();
    }

    public async Task<bool> HasLegendAsync()
    {
        var legend = Page.Locator(".map-legend");
        return await legend.IsVisibleAsync();
    }

    public async Task<bool> HasProtectionLevelsInLegendAsync()
    {
        var noTake = Page.GetByText("No-Take Zone");
        var highlyProtected = Page.GetByText("Highly Protected");
        var lightlyProtected = Page.GetByText("Lightly Protected");

        var hasNoTake = await noTake.IsVisibleAsync();
        var hasHighly = await highlyProtected.IsVisibleAsync();
        var hasLightly = await lightlyProtected.IsVisibleAsync();

        return hasNoTake && hasHighly && hasLightly;
    }

    public async Task<bool> HasLoadingOverlayDisappearedAsync()
    {
        var loadingOverlay = Page.Locator(".loading-overlay");
        // Should either not exist or not be visible
        var isVisible = await loadingOverlay.IsVisibleAsync();
        return !isVisible;
    }

    public async Task ClickOnMapCenterAsync()
    {
        var mapContainer = Page.Locator(".leaflet-map-container, .leaflet-container").First;
        await mapContainer.ClickAsync();
    }

    public async Task<bool> HasMpaInfoPopupAfterClickAsync()
    {
        // Click on the map to try to select an MPA
        await ClickOnMapCenterAsync();
        await Task.Delay(500);

        var infoPopup = Page.Locator(".map-info-popup");
        return await infoPopup.IsVisibleAsync();
    }

    public async Task<ILocator> GetMapContainerAsync()
    {
        return Page.Locator(".leaflet-map-container, .leaflet-container").First;
    }

    /// <summary>
    /// Check if MPA boundaries (SVG polygons) are visible on the map
    /// </summary>
    public async Task<bool> HasMpaBoundariesAsync()
    {
        // Leaflet renders GeoJSON features as SVG paths in the overlay-pane
        var svgPaths = Page.Locator(".leaflet-overlay-pane svg path");
        var count = await svgPaths.CountAsync();
        return count > 0;
    }

    /// <summary>
    /// Get the count of MPA boundary polygons on the map
    /// </summary>
    public async Task<int> GetMpaPolygonCountAsync()
    {
        var svgPaths = Page.Locator(".leaflet-overlay-pane svg path");
        return await svgPaths.CountAsync();
    }

    /// <summary>
    /// Check if the MPA count badge is visible
    /// </summary>
    public async Task<bool> HasMpaCountBadgeAsync()
    {
        var badge = Page.Locator(".mpa-count-badge");
        return await badge.IsVisibleAsync();
    }

    /// <summary>
    /// Get the MPA count from the badge
    /// </summary>
    public async Task<string> GetMpaCountTextAsync()
    {
        var badge = Page.Locator(".mpa-count-badge");
        return await badge.TextContentAsync() ?? "";
    }

    public async Task<bool> CanZoomAsync()
    {
        // Check if map responds to scroll/zoom
        var mapContainer = await GetMapContainerAsync();
        var initialBoundingBox = await mapContainer.BoundingBoxAsync();

        // Scroll to zoom
        await mapContainer.HoverAsync();
        await Page.Mouse.WheelAsync(0, -100); // Zoom in
        await Task.Delay(300);

        // Map should still be visible and responsive
        return await IsMapContainerVisibleAsync();
    }

    public async Task<bool> CanPanAsync()
    {
        var mapContainer = await GetMapContainerAsync();

        // Drag to pan
        var box = await mapContainer.BoundingBoxAsync();
        if (box == null) return false;

        var startX = box.X + box.Width / 2;
        var startY = box.Y + box.Height / 2;

        await Page.Mouse.MoveAsync((float)startX, (float)startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)(startX + 50), (float)(startY + 50));
        await Page.Mouse.UpAsync();
        await Task.Delay(300);

        // Map should still be visible
        return await IsMapContainerVisibleAsync();
    }

    public async Task<int> GetLegendItemCountAsync()
    {
        var legendItems = Page.Locator(".map-legend .legend-item");
        return await legendItems.CountAsync();
    }

    // ============ NOAA Bleaching Data Methods ============

    /// <summary>
    /// Check if NOAA bleaching data is displayed in the info panel
    /// </summary>
    public async Task<bool> HasNoaaDataDisplayedAsync()
    {
        // Look for SST card or DHW value
        var sstCard = Page.Locator(".card.bg-light").First;
        var dhwValue = Page.GetByText("DHW").Or(Page.Locator("[class*='dhw']"));
        return await sstCard.IsVisibleAsync() || await dhwValue.IsVisibleAsync();
    }

    /// <summary>
    /// Get the Sea Surface Temperature value from the info panel
    /// </summary>
    public async Task<string> GetSstValueAsync()
    {
        // Wait a short moment then try to get SST value if visible
        var sstCard = Page.Locator(".card.bg-light h3, .sst-value, [class*='sst']").First;
        try
        {
            if (await sstCard.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 5000 }))
            {
                return await sstCard.TextContentAsync() ?? "";
            }
        }
        catch { }

        // Also try to find by text pattern
        var tempText = Page.GetByText(new System.Text.RegularExpressions.Regex(@"\d+\.?\d*\s*°C")).First;
        try
        {
            if (await tempText.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 2000 }))
            {
                return await tempText.TextContentAsync() ?? "";
            }
        }
        catch { }

        return "";
    }

    /// <summary>
    /// Get the Degree Heating Week value from the info panel
    /// </summary>
    public async Task<string> GetDhwValueAsync()
    {
        var dhwValue = Page.Locator(".dhw-value, .card.bg-light:has-text('DHW') h3").First;
        return await dhwValue.TextContentAsync() ?? "";
    }

    /// <summary>
    /// Check if NOAA error message with retry button is visible
    /// </summary>
    public async Task<bool> HasNoaaErrorMessageAsync()
    {
        var errorMessage = Page.Locator(".bleaching-error, .alert-warning:has-text('NOAA')");
        var retryButton = Page.GetByRole(AriaRole.Button, new() { Name = "Retry" });
        return await errorMessage.IsVisibleAsync() || await retryButton.IsVisibleAsync();
    }

    /// <summary>
    /// Check if NOAA loading spinner is visible
    /// </summary>
    public async Task<bool> HasNoaaLoadingSpinnerAsync()
    {
        var spinner = Page.Locator(".bleaching-loading, .spinner-border").First;
        return await spinner.IsVisibleAsync();
    }

    /// <summary>
    /// Click the retry button for NOAA data
    /// </summary>
    public async Task ClickNoaaRetryAsync()
    {
        var retryButton = Page.GetByRole(AriaRole.Button, new() { Name = "Retry" });
        if (await retryButton.IsVisibleAsync())
        {
            await retryButton.ClickAsync();
        }
    }

    // ============ Fishing Events Methods ============

    /// <summary>
    /// Toggle the fishing activity layer on/off
    /// </summary>
    public async Task ToggleFishingActivityAsync()
    {
        var fishingToggle = Page.GetByLabel("Fishing Activity");
        if (await fishingToggle.IsVisibleAsync())
        {
            await fishingToggle.ClickAsync();
        }
    }

    /// <summary>
    /// Check if fishing markers (circle markers) are visible on the map
    /// </summary>
    public async Task<bool> HasFishingMarkersAsync()
    {
        // Leaflet circle markers are rendered as SVG circles or in a marker pane
        var circleMarkers = Page.Locator(".leaflet-marker-pane img, .leaflet-overlay-pane circle, .fishing-marker");
        var count = await circleMarkers.CountAsync();
        return count > 0;
    }

    /// <summary>
    /// Get count of fishing markers on the map
    /// </summary>
    public async Task<int> GetFishingMarkerCountAsync()
    {
        var circleMarkers = Page.Locator(".leaflet-marker-pane img, .leaflet-overlay-pane circle, .fishing-marker");
        return await circleMarkers.CountAsync();
    }

    /// <summary>
    /// Select a fishing time filter (7, 14, or 30 days)
    /// </summary>
    public async Task SelectFishingTimeFilterAsync(int days)
    {
        var filterButton = Page.GetByRole(AriaRole.Button, new() { Name = $"{days}d" });
        if (await filterButton.IsVisibleAsync())
        {
            await filterButton.ClickAsync();
        }
    }

    /// <summary>
    /// Check if the fishing events badge is visible
    /// </summary>
    public async Task<bool> HasFishingEventsBadgeAsync()
    {
        var badge = Page.Locator(".fishing-events-badge");
        return await badge.IsVisibleAsync();
    }

    /// <summary>
    /// Get the fishing events count from the badge
    /// </summary>
    public async Task<string> GetFishingEventsCountTextAsync()
    {
        var badge = Page.Locator(".fishing-events-badge");
        return await badge.TextContentAsync() ?? "";
    }

    // ============ MPA Selection & Info Panel Methods ============

    /// <summary>
    /// Click on an MPA in the sidebar list by name
    /// </summary>
    public async Task SelectMpaFromSidebarAsync(string mpaName)
    {
        var mpaItem = Page.Locator($".list-group-item:has-text('{mpaName}')").First;
        if (await mpaItem.IsVisibleAsync())
        {
            await mpaItem.ClickAsync();
        }
    }

    /// <summary>
    /// Get the currently selected MPA name from the info panel
    /// </summary>
    public async Task<string> GetSelectedMpaNameAsync()
    {
        var cardTitle = Page.Locator(".card-title").First;
        return await cardTitle.TextContentAsync() ?? "";
    }

    /// <summary>
    /// Check if the info panel shows protection level badge
    /// </summary>
    public async Task<bool> HasProtectionLevelBadgeAsync()
    {
        // Look for badge with protection-related text or any badge in the info panel
        var badge = Page.Locator(".badge, .badge-primary, .badge-danger, .badge-warning, .badge-success").First;
        if (await badge.IsVisibleAsync()) return true;

        // Also check for protection level text
        var protectionText = Page.GetByText("No-Take").Or(Page.GetByText("Highly Protected")).Or(Page.GetByText("Lightly Protected"));
        return await protectionText.First.IsVisibleAsync();
    }

    /// <summary>
    /// Check if the MPA area is displayed in the info panel
    /// </summary>
    public async Task<bool> HasMpaAreaDisplayedAsync()
    {
        var areaText = Page.GetByText("km²").Or(Page.GetByText("sq km")).First;
        return await areaText.IsVisibleAsync();
    }

    /// <summary>
    /// Check if an MPA polygon is highlighted (selected)
    /// </summary>
    public async Task<bool> HasHighlightedMpaAsync()
    {
        // Selected MPA typically has yellow/different stroke color
        var highlightedPath = Page.Locator(".leaflet-overlay-pane svg path[stroke*='yellow'], .leaflet-overlay-pane svg path.selected");
        var count = await highlightedPath.CountAsync();
        return count > 0;
    }

    // ============ List View Methods ============

    /// <summary>
    /// Switch to List View
    /// </summary>
    public async Task SwitchToListViewAsync()
    {
        var listViewButton = Page.GetByRole(AriaRole.Button, new() { Name = "List View" });
        await listViewButton.ClickAsync();
    }

    /// <summary>
    /// Switch to Map View
    /// </summary>
    public async Task SwitchToMapViewAsync()
    {
        var mapViewButton = Page.GetByRole(AriaRole.Button, new() { Name = "Map View" });
        await mapViewButton.ClickAsync();
    }

    /// <summary>
    /// Check if the MPA list table is visible
    /// </summary>
    public async Task<bool> HasMpaListTableAsync()
    {
        var table = Page.Locator("table, .list-group, .mpa-list");
        return await table.IsVisibleAsync();
    }

    /// <summary>
    /// Get the count of MPAs in the list view
    /// </summary>
    public async Task<int> GetMpaListCountAsync()
    {
        var rows = Page.Locator("table tbody tr, .list-group-item");
        return await rows.CountAsync();
    }

    /// <summary>
    /// Click on an MPA row in the list view
    /// </summary>
    public async Task ClickMpaListRowAsync(int index)
    {
        var row = Page.Locator("table tbody tr, .list-group-item").Nth(index);
        if (await row.IsVisibleAsync())
        {
            await row.ClickAsync();
        }
    }

    // ============ Screenshot Helpers ============

    /// <summary>
    /// Capture a screenshot of the current map state
    /// </summary>
    public async Task<string> CaptureScreenshotAsync(string filename)
    {
        var screenshotDir = System.IO.Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "playwright-artifacts");
        Directory.CreateDirectory(screenshotDir);

        var screenshotPath = System.IO.Path.Combine(screenshotDir, filename);
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
        return screenshotPath;
    }

    /// <summary>
    /// Check if MPA polygons have the expected protection level colors
    /// </summary>
    public async Task<bool> HasProtectionLevelColorsAsync()
    {
        // Check for different fill colors on SVG paths
        // No-Take: red (#dc3545), Highly Protected: orange (#fd7e14), etc.
        var svgPaths = Page.Locator(".leaflet-overlay-pane svg path");
        var count = await svgPaths.CountAsync();

        if (count == 0) return false;

        // Check if paths have fill attributes with different colors
        var pathStyles = new HashSet<string>();
        for (int i = 0; i < Math.Min(count, 8); i++)
        {
            var path = svgPaths.Nth(i);
            var fill = await path.GetAttributeAsync("fill") ?? "";
            var style = await path.GetAttributeAsync("style") ?? "";
            pathStyles.Add(fill + style);
        }

        // Should have multiple different styles (different protection levels)
        return pathStyles.Count > 1;
    }
}
