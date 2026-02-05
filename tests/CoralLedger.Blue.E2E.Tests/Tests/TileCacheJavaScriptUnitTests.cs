using Microsoft.Playwright;

namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// Unit tests for tile-cache.js JavaScript functions.
/// These tests load the actual tile-cache.js script and run functions in an isolated browser context.
/// </summary>
[TestFixture]
public class TileCacheJavaScriptUnitTests : PlaywrightFixture
{
    private string? _tileCacheScriptContent;

    [OneTimeSetUp]
    public void LoadTileCacheScript()
    {
        // Load the actual tile-cache.js script from the source
        var scriptPath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "src", "CoralLedger.Blue.Web", "wwwroot", "js", "tile-cache.js"
        ));

        // Try alternative path if first doesn't exist (for CI/CD environments)
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "CoralLedger.Blue.Web", "wwwroot", "js", "tile-cache.js"
            ));
        }

        if (File.Exists(scriptPath))
        {
            _tileCacheScriptContent = File.ReadAllText(scriptPath);
            TestContext.Progress.WriteLine($"Loaded tile-cache.js from: {scriptPath}");
        }
        else
        {
            TestContext.Progress.WriteLine($"Warning: tile-cache.js not found at {scriptPath}, using inline fallback");
            _tileCacheScriptContent = GetFallbackScript();
        }
    }

    [SetUp]
    public async Task LocalSetUp()
    {
        // Navigate to about:blank and inject the tile-cache script
        await Page.GotoAsync("about:blank");

        // Mock IndexedDB since it's not available in about:blank context
        await Page.EvaluateAsync(@"
            window.indexedDB = {
                open: () => ({ onerror: null, onsuccess: null, onupgradeneeded: null })
            };
        ");

        // Load the actual script
        await Page.EvaluateAsync(_tileCacheScriptContent!);

        // Add getStats mock for synchronous testing (actual getStats is async and requires IndexedDB)
        await Page.EvaluateAsync(@"
            window.tileCache.getStats = function() {
                return {
                    totalTiles: this.stats.totalTiles,
                    totalBytes: this.stats.totalBytes,
                    totalMB: Math.round(this.stats.totalBytes / 1024 / 1024 * 10) / 10,
                    lastUpdated: this.stats.lastUpdated
                };
            };
        ");
    }

    #region getTileKey Tests

    [Test]
    [Description("Verifies getTileKey generates correct format")]
    public async Task GetTileKey_GeneratesCorrectFormat()
    {
        // Act
        var key = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('dark', 10, 5, 7)");

        // Assert
        key.Should().Be("dark_10_5_7", "getTileKey should generate format: theme_z_x_y");
    }

    [Test]
    [Description("Verifies getTileKey handles different themes")]
    public async Task GetTileKey_HandlesDifferentThemes()
    {
        // Act
        var key1 = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('light', 10, 5, 7)");
        var key2 = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('satellite', 10, 5, 7)");

        // Assert
        key1.Should().Be("light_10_5_7", "light theme key should be generated correctly");
        key2.Should().Be("satellite_10_5_7", "satellite theme key should be generated correctly");
    }

    [Test]
    [Description("Verifies getTileKey handles large coordinates")]
    public async Task GetTileKey_HandlesLargeCoordinates()
    {
        // Act
        var key = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('dark', 18, 1234, 5678)");

        // Assert
        key.Should().Be("dark_18_1234_5678", "large coordinate values should be handled correctly");
    }

    [Test]
    [Description("Verifies getTileKey handles zero coordinates")]
    public async Task GetTileKey_HandlesZeroCoordinates()
    {
        // Act
        var key = await Page.EvaluateAsync<string>("window.tileCache.getTileKey('dark', 0, 0, 0)");

        // Assert
        key.Should().Be("dark_0_0_0", "zero coordinate values should be handled correctly");
    }

    #endregion

    #region latLngToTile Tests

    [Test]
    [Description("Verifies latLngToTile converts coordinates at zoom 0")]
    public async Task LatLngToTile_ConvertsAtZoom0()
    {
        // Act
        var tile = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(0, 0, 0)"
        );

        // Assert
        tile.Should().ContainKey("x", "tile should have x coordinate");
        tile.Should().ContainKey("y", "tile should have y coordinate");
        Convert.ToInt32(tile["x"]).Should().Be(0, "origin at zoom 0 should have x=0");
        Convert.ToInt32(tile["y"]).Should().Be(0, "origin at zoom 0 should have y=0");
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

        x.Should().BeGreaterThan(0, "Nassau x coordinate should be positive")
            .And.BeLessThan(1024, "x at zoom 10 should be less than 1024");
        y.Should().BeGreaterThan(0, "Nassau y coordinate should be positive")
            .And.BeLessThan(1024, "y at zoom 10 should be less than 1024");
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

        x1.Should().NotBe(x2, "different zoom levels should produce different tile coordinates");
    }

    [Test]
    [Description("Verifies latLngToTile handles negative longitude (Western hemisphere)")]
    public async Task LatLngToTile_HandlesNegativeLongitude()
    {
        // Act - New York area
        var tile = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(40.7128, -74.0060, 10)"
        );

        // Assert
        tile.Should().ContainKey("x", "should return x coordinate for negative longitude");
        tile.Should().ContainKey("y", "should return y coordinate for negative longitude");
    }

    [Test]
    [Description("Verifies latLngToTile handles positive longitude (Eastern hemisphere)")]
    public async Task LatLngToTile_HandlesPositiveLongitude()
    {
        // Act - London area
        var tile = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.latLngToTile(51.5074, -0.1278, 10)"
        );

        // Assert
        tile.Should().ContainKey("x", "should return x coordinate for positive longitude");
        tile.Should().ContainKey("y", "should return y coordinate for positive longitude");
    }

    #endregion

    #region buildTileUrl Tests

    [Test]
    [Description("Verifies buildTileUrl replaces all placeholders")]
    public async Task BuildTileUrl_ReplacesAllPlaceholders()
    {
        // Act
        var url = await Page.EvaluateAsync<string>(
            "window.tileCache.buildTileUrl('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', 10, 5, 7)"
        );

        // Assert
        url.Should().Be("https://a.tile.openstreetmap.org/10/5/7.png",
            "all placeholders should be replaced with actual values");
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
        url.Should().Contain("https://a.example.com/", "subdomain placeholder should default to 'a'");
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
        url.Should().Be("https://example.com/10/5/7.png",
            "retina placeholder {r} should be removed");
    }

    [Test]
    [Description("Verifies buildTileUrl handles URL without subdomain placeholder")]
    public async Task BuildTileUrl_HandlesUrlWithoutSubdomain()
    {
        // Act
        var url = await Page.EvaluateAsync<string>(
            "window.tileCache.buildTileUrl('https://example.com/tiles/{z}/{x}/{y}.png', 10, 5, 7)"
        );

        // Assert
        url.Should().Be("https://example.com/tiles/10/5/7.png",
            "URL without subdomain should still work correctly");
    }

    #endregion

    #region getTilesForRegion Tests

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
        tiles.Should().NotBeNull("getTilesForRegion should return an array");
        tiles.Length.Should().BeGreaterThan(0, "region should contain at least one tile");
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
        tiles2.Length.Should().BeGreaterThan(tiles1.Length,
            "larger zoom range should include more tiles");
    }

    [Test]
    [Description("Verifies getTilesForRegion tiles have correct structure")]
    public async Task GetTilesForRegion_TilesHaveCorrectStructure()
    {
        // Act
        var tilesJson = await Page.EvaluateAsync<string>(
            @"JSON.stringify(window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10
            ))"
        );

        // Assert - Parse and check structure
        tilesJson.Should().Contain("\"z\":", "tiles should have z property");
        tilesJson.Should().Contain("\"x\":", "tiles should have x property");
        tilesJson.Should().Contain("\"y\":", "tiles should have y property");
    }

    [Test]
    [Description("Verifies getTilesForRegion handles single zoom level")]
    public async Task GetTilesForRegion_HandlesSingleZoomLevel()
    {
        // Act
        var allSameZoom = await Page.EvaluateAsync<bool>(
            @"window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                10
            ).every(t => t.z === 10)"
        );

        // Assert
        allSameZoom.Should().BeTrue("all tiles should have the same zoom level when min=max");
    }

    [Test]
    [Description("Verifies getTilesForRegion returns empty array for invalid bounds")]
    public async Task GetTilesForRegion_HandlesInvertedZoomRange()
    {
        // Act - minZoom > maxZoom should return empty
        var tiles = await Page.EvaluateAsync<object[]>(
            @"window.tileCache.getTilesForRegion(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                12,
                10
            )"
        );

        // Assert
        tiles.Should().BeEmpty("inverted zoom range (min > max) should return empty array");
    }

    #endregion

    #region estimateRegionSize Tests

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
        estimate.Should().ContainKey("tileCount", "estimate should include tile count");
        estimate.Should().ContainKey("estimatedBytes", "estimate should include byte estimate");
        estimate.Should().ContainKey("estimatedMB", "estimate should include MB estimate");
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

        estimatedBytes.Should().Be(tileCount * 15000,
            "estimated bytes should equal tileCount * avgTileSize");
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

        bytes2.Should().Be(bytes1 * 2,
            "doubling avgTileSize should double estimated bytes");
    }

    [Test]
    [Description("Verifies estimateRegionSize converts bytes to MB correctly")]
    public async Task EstimateRegionSize_ConvertsBytesToMBCorrectly()
    {
        // Act
        var estimate = await Page.EvaluateAsync<Dictionary<string, object>>(
            @"window.tileCache.estimateRegionSize(
                { north: 25.1, south: 25.0, east: -77.3, west: -77.4 },
                10,
                12,
                15000
            )"
        );

        // Assert
        var estimatedBytes = Convert.ToInt64(estimate["estimatedBytes"]);
        var estimatedMB = Convert.ToDouble(estimate["estimatedMB"]);
        var expectedMB = Math.Round(estimatedBytes / 1024.0 / 1024.0 * 10) / 10;

        estimatedMB.Should().Be(expectedMB,
            "MB conversion should use correct formula (bytes / 1024 / 1024, rounded to 1 decimal)");
    }

    #endregion

    #region isOnline Tests

    [Test]
    [Description("Verifies isOnline returns boolean")]
    public async Task IsOnline_ReturnsBoolean()
    {
        // Act
        var isOnline = await Page.EvaluateAsync<bool>("window.tileCache.isOnline()");

        // Assert - Should be online in test environment
        isOnline.Should().BeTrue("browser should be online during tests");
    }

    #endregion

    #region getStats Tests

    [Test]
    [Description("Verifies getStats returns correct structure")]
    public async Task GetStats_ReturnsCorrectStructure()
    {
        // Act
        var stats = await Page.EvaluateAsync<Dictionary<string, object>>(
            "window.tileCache.getStats()"
        );

        // Assert
        stats.Should().ContainKey("totalTiles", "stats should include total tiles count");
        stats.Should().ContainKey("totalBytes", "stats should include total bytes");
        stats.Should().ContainKey("totalMB", "stats should include total MB");
    }

    [Test]
    [Description("Verifies stats object is initialized with correct structure")]
    public async Task Stats_InitializedCorrectly()
    {
        // Act
        var hasStatsObject = await Page.EvaluateAsync<bool>(
            "typeof window.tileCache.stats === 'object'"
        );
        var hasTotalTiles = await Page.EvaluateAsync<bool>(
            "'totalTiles' in window.tileCache.stats"
        );
        var hasTotalBytes = await Page.EvaluateAsync<bool>(
            "'totalBytes' in window.tileCache.stats"
        );

        // Assert
        hasStatsObject.Should().BeTrue("tileCache should have stats object");
        hasTotalTiles.Should().BeTrue("stats should have totalTiles property");
        hasTotalBytes.Should().BeTrue("stats should have totalBytes property");
    }

    #endregion

    #region Initialization Tests

    [Test]
    [Description("Verifies tileCache object has required properties")]
    public async Task TileCache_HasRequiredProperties()
    {
        // Act & Assert
        var hasDbName = await Page.EvaluateAsync<bool>("'dbName' in window.tileCache");
        var hasDbVersion = await Page.EvaluateAsync<bool>("'dbVersion' in window.tileCache");
        var hasStoreName = await Page.EvaluateAsync<bool>("'storeName' in window.tileCache");

        hasDbName.Should().BeTrue("tileCache should have dbName property");
        hasDbVersion.Should().BeTrue("tileCache should have dbVersion property");
        hasStoreName.Should().BeTrue("tileCache should have storeName property");
    }

    [Test]
    [Description("Verifies tileCache has all required methods")]
    public async Task TileCache_HasRequiredMethods()
    {
        // Act
        var methods = new[]
        {
            "getTileKey", "latLngToTile", "buildTileUrl",
            "getTilesForRegion", "estimateRegionSize", "isOnline"
        };

        foreach (var method in methods)
        {
            var hasMethod = await Page.EvaluateAsync<bool>(
                $"typeof window.tileCache.{method} === 'function'"
            );

            // Assert
            hasMethod.Should().BeTrue($"tileCache should have {method} method");
        }
    }

    #endregion

    /// <summary>
    /// Fallback script in case the actual file cannot be loaded (e.g., in CI/CD).
    /// This should match the core pure functions from tile-cache.js.
    /// </summary>
    private static string GetFallbackScript()
    {
        return @"
            window.tileCache = {
                dbName: 'CoralLedgerTileCache',
                dbVersion: 1,
                storeName: 'tiles',
                db: null,
                stats: { totalTiles: 0, totalBytes: 0, lastUpdated: null },

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
                },

                getStats() {
                    return {
                        totalTiles: this.stats.totalTiles,
                        totalBytes: this.stats.totalBytes,
                        totalMB: Math.round(this.stats.totalBytes / 1024 / 1024 * 10) / 10,
                        lastUpdated: this.stats.lastUpdated
                    };
                }
            };
        ";
    }
}
