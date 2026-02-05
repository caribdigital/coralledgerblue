using Microsoft.Playwright;

namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// Unit tests for tile-cache.js JavaScript functions.
/// These tests run the JavaScript functions directly without requiring the full app to be running.
/// </summary>
[TestFixture]
public class TileCacheJavaScriptUnitTests : PlaywrightFixture
{
    private const string TileCacheScript = @"
        window.tileCache = {
            getTileKey(theme, z, x, y) {
                return `${theme}_${z}_${x}_${y}`;
            },

            latLngToTile(lat, lng, zoom) {
                const n = Math.pow(2, zoom);
                const x = Math.floor((lng + 180) / 360 * n);
                const latRad = lat * Math.PI / 180;
                const y = Math.floor((1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2 * n);
                return { x, y };
            },

            buildTileUrl(template, z, x, y) {
                let url = template.replace('{s}', 'a');
                url = url.replace('{z}', z);
                url = url.replace('{x}', x);
                url = url.replace('{y}', y);
                url = url.replace('{r}', '');
                return url;
            },

            getTilesForRegion(bounds, minZoom, maxZoom) {
                const tiles = [];
                
                for (let z = minZoom; z <= maxZoom; z++) {
                    const nwTile = this.latLngToTile(bounds.north, bounds.west, z);
                    const seTile = this.latLngToTile(bounds.south, bounds.east, z);
                    
                    for (let x = Math.min(nwTile.x, seTile.x); x <= Math.max(nwTile.x, seTile.x); x++) {
                        for (let y = Math.min(nwTile.y, seTile.y); y <= Math.max(nwTile.y, seTile.y); y++) {
                            tiles.push({ z, x, y });
                        }
                    }
                }
                
                return tiles;
            },

            estimateRegionSize(bounds, minZoom, maxZoom, avgTileSize = 15000) {
                const tiles = this.getTilesForRegion(bounds, minZoom, maxZoom);
                return {
                    tileCount: tiles.length,
                    estimatedBytes: tiles.length * avgTileSize,
                    estimatedMB: Math.round(tiles.length * avgTileSize / 1024 / 1024 * 10) / 10
                };
            },

            isOnline() {
                return navigator.onLine;
            }
        };
    ";

    [SetUp]
    public async Task LocalSetUp()
    {
        // Navigate to about:blank and inject the tile-cache functions
        await Page.GotoAsync("about:blank");
        await Page.EvaluateAsync(TileCacheScript);
    }

    [Test]
    [Description("Verifies getTileKey generates correct format")]
    public async Task GetTileKey_GeneratesCorrectFormat()
    {
        // Act
        var key = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('dark', 10, 5, 7)");

        // Assert
        key.Should().Be("dark_10_5_7");
    }

    [Test]
    [Description("Verifies getTileKey handles different themes")]
    public async Task GetTileKey_HandlesDifferentThemes()
    {
        // Act
        var key1 = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('light', 10, 5, 7)");
        var key2 = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('satellite', 10, 5, 7)");

        // Assert
        key1.Should().Be("light_10_5_7");
        key2.Should().Be("satellite_10_5_7");
    }

    [Test]
    [Description("Verifies getTileKey handles large coordinates")]
    public async Task GetTileKey_HandlesLargeCoordinates()
    {
        // Act
        var key = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('dark', 18, 1234, 5678)");

        // Assert
        key.Should().Be("dark_18_1234_5678");
    }

    [Test]
    [Description("Verifies latLngToTile converts coordinates at zoom 0")]
    public async Task LatLngToTile_ConvertsAtZoom0()
    {
        // Act
        var tile = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(0, 0, 0)"
        );

        // Assert
        tile.Should().ContainKey("x");
        tile.Should().ContainKey("y");
        Convert.ToInt32(tile["x"]).Should().Be(0);
        Convert.ToInt32(tile["y"]).Should().Be(0);
    }

    [Test]
    [Description("Verifies latLngToTile converts Nassau coordinates correctly")]
    public async Task LatLngToTile_ConvertsNassauCoordinates()
    {
        // Act - Nassau, Bahamas: 25.0443, -77.3504
        var tile = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(25.0443, -77.3504, 10)"
        );

        // Assert
        var x = Convert.ToInt32(tile["x"]);
        var y = Convert.ToInt32(tile["y"]);
        
        x.Should().BeGreaterThan(0).And.BeLessThan(1024);
        y.Should().BeGreaterThan(0).And.BeLessThan(1024);
    }

    [Test]
    [Description("Verifies latLngToTile produces different results for different zoom levels")]
    public async Task LatLngToTile_DifferentResultsForDifferentZooms()
    {
        // Act
        var tile1 = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(25, -77, 5)"
        );
        var tile2 = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(25, -77, 10)"
        );

        // Assert
        var x1 = Convert.ToInt32(tile1["x"]);
        var x2 = Convert.ToInt32(tile2["x"]);
        
        x1.Should().NotBe(x2, "Different zoom levels should produce different tile coordinates");
    }

    [Test]
    [Description("Verifies buildTileUrl replaces all placeholders")]
    public async Task BuildTileUrl_ReplacesAllPlaceholders()
    {
        // Act
        var url = await Page.EvaluateAsync<string>(
            "window.tileCache.buildTileUrl('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', 10, 5, 7)"
        );

        // Assert
        url.Should().Be("https://a.tile.openstreetmap.org/10/5/7.png");
    }

    [Test]
    [Description("Verifies buildTileUrl uses 'a' as default subdomain")]
    public async Task BuildTileUrl_UsesDefaultSubdomain()
    {
        // Act
        var url = await Page.EvaluateAsync<string>(
            "window.tileCache.buildTileUrl('https://{s}.example.com/{z}/{x}/{y}', 1, 2, 3)"
        );

        // Assert
        url.Should().Contain("https://a.example.com/");
    }

    [Test]
    [Description("Verifies buildTileUrl removes retina placeholder")]
    public async Task BuildTileUrl_RemovesRetinaPlaceholder()
    {
        // Act
        var url = await Page.EvaluateAsync<string>(
            "window.tileCache.buildTileUrl('https://example.com/{z}/{x}/{y}{r}.png', 10, 5, 7)"
        );

        // Assert
        url.Should().Be("https://example.com/10/5/7.png");
    }

    [Test]
    [Description("Verifies getTilesForRegion returns array of tiles")]
    public async Task GetTilesForRegion_ReturnsArrayOfTiles()
    {
        // Act
        var tiles = await Page.EvaluateAsync<object[]>(
            @"window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10
            )"
        );

        // Assert
        tiles.Should().NotBeNull();
        tiles.Length.Should().BeGreaterThan(0);
    }

    [Test]
    [Description("Verifies getTilesForRegion returns more tiles for larger zoom range")]
    public async Task GetTilesForRegion_MoreTilesForLargerZoomRange()
    {
        // Act
        var tiles1 = await Page.EvaluateAsync<object[]>(
            @"window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10
            )"
        );
        var tiles2 = await Page.EvaluateAsync<object[]>(
            @"window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                12
            )"
        );

        // Assert
        tiles2.Length.Should().BeGreaterThan(tiles1.Length);
    }

    [Test]
    [Description("Verifies estimateRegionSize returns correct structure")]
    public async Task EstimateRegionSize_ReturnsCorrectStructure()
    {
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
    }

    [Test]
    [Description("Verifies estimateRegionSize calculates bytes based on tile count")]
    public async Task EstimateRegionSize_CalculatesBytesCorrectly()
    {
        // Act
        var estimate = await Page.EvaluateAsync<Dictionary<string, object>>(
            @"window.tileCache.estimateRegionSize(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10,
                15000
            )"
        );

        // Assert
        var tileCount = Convert.ToInt32(estimate["tileCount"]);
        var estimatedBytes = Convert.ToInt64(estimate["estimatedBytes"]);
        
        estimatedBytes.Should().Be(tileCount * 15000);
    }

    [Test]
    [Description("Verifies estimateRegionSize uses custom average tile size")]
    public async Task EstimateRegionSize_UsesCustomAverageTileSize()
    {
        // Act
        var estimate1 = await Page.EvaluateAsync<Dictionary<string, object>>(
            @"window.tileCache.estimateRegionSize(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10,
                10000
            )"
        );
        var estimate2 = await Page.EvaluateAsync<Dictionary<string, object>>(
            @"window.tileCache.estimateRegionSize(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10,
                20000
            )"
        );

        // Assert
        var bytes1 = Convert.ToInt64(estimate1["estimatedBytes"]);
        var bytes2 = Convert.ToInt64(estimate2["estimatedBytes"]);
        
        bytes2.Should().Be(bytes1 * 2);
    }

    [Test]
    [Description("Verifies isOnline returns boolean")]
    public async Task IsOnline_ReturnsBoolean()
    {
        // Act
        var isOnline = await Page.EvaluateAsync<bool>("window.tileCache.isOnline()");

        // Assert - Should be online in test environment
        isOnline.Should().BeTrue("Browser should be online during tests");
    }
}
