using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.IntegrationTests;

/// <summary>
/// Integration tests for the OfflineMapManager component.
/// Tests verify the component renders and integrates correctly with the map page.
/// </summary>
public class OfflineMapManagerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public OfflineMapManagerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MapPage_RendersOfflineMapManager()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("offline-map-manager", "OfflineMapManager component should be present on map page");
    }

    [Fact]
    public async Task OfflineMapManager_ContainsCacheStatisticsSection()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Cache Statistics", "Component should display cache statistics section");
        content.Should().Contain("Total Tiles", "Component should show total tiles stat");
        content.Should().Contain("Storage Used", "Component should show storage used stat");
    }

    [Fact]
    public async Task OfflineMapManager_ContainsDownloadSection()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Download Map Tiles", "Component should have download section");
        content.Should().Contain("Theme", "Component should have theme selector");
        content.Should().Contain("Min Zoom", "Component should have min zoom input");
        content.Should().Contain("Max Zoom", "Component should have max zoom input");
    }

    [Fact]
    public async Task OfflineMapManager_ContainsEstimateButton()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Estimate Current View", "Component should have estimate button");
    }

    [Fact]
    public async Task OfflineMapManager_ContainsDownloadButton()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Download Current View", "Component should have download button");
    }

    [Fact]
    public async Task OfflineMapManager_ContainsManagementActions()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Manage Cache", "Component should have management section");
        content.Should().Contain("Clear Tiles Older Than 30 Days", "Component should have clear old tiles button");
        content.Should().Contain("Clear All Cache", "Component should have clear all button");
    }

    [Fact]
    public async Task OfflineMapManager_ContainsRefreshButton()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Refresh", "Component should have refresh button");
    }

    [Fact]
    public async Task OfflineMapManager_HasThemeOptions()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Dark (CartoDB)", "Component should have dark theme option");
        content.Should().Contain("Light (OpenStreetMap)", "Component should have light theme option");
        content.Should().Contain("Satellite (Esri)", "Component should have satellite theme option");
    }

    [Fact]
    public async Task MapPage_IncludesTileCacheScript()
    {
        // Act
        var response = await _client.GetAsync("/map");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("tile-cache.js", "Page should include tile-cache.js script");
    }

    [Fact]
    public async Task TileCacheScript_IsAccessible()
    {
        // Act
        var response = await _client.GetAsync("/js/tile-cache.js");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("window.tileCache", "tile-cache.js should define window.tileCache object");
        content.Should().Contain("getTileKey", "tile-cache.js should have getTileKey function");
        content.Should().Contain("latLngToTile", "tile-cache.js should have latLngToTile function");
        content.Should().Contain("getTilesForRegion", "tile-cache.js should have getTilesForRegion function");
        content.Should().Contain("estimateRegionSize", "tile-cache.js should have estimateRegionSize function");
    }

    [Fact]
    public async Task LeafletMapScript_IsAccessible()
    {
        // Act
        var response = await _client.GetAsync("/js/leaflet-map.js");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
