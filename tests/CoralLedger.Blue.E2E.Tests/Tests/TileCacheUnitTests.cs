using Microsoft.Playwright;

namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E test that runs JavaScript unit tests for tile-cache.js using an HTML test runner.
/// This approach allows us to test JavaScript functions directly in a browser context.
/// </summary>
[TestFixture]
public class TileCacheUnitTests : PlaywrightFixture
{
    [Test]
    [Description("Runs all JavaScript unit tests for tile-cache.js")]
    public async Task TileCache_JavaScriptUnitTests_AllPass()
    {
        // Arrange
        var testFilePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Tests",
            "tile-cache-unit-tests.html"
        );

        // Verify test file exists
        File.Exists(testFilePath).Should().BeTrue($"Test file should exist at {testFilePath}");

        // Act - Navigate to the test HTML file
        await Page.GotoAsync($"file://{testFilePath}");
        
        // Wait for tests to complete (wait for test results to be available)
        await Page.WaitForFunctionAsync(
            "() => window.testResults !== undefined",
            new PageWaitForFunctionOptions { Timeout = 30000 }
        );

        // Get test results
        var results = await Page.EvaluateAsync<TestResults>("window.testResults");

        // Assert - All tests should pass
        results.Should().NotBeNull();
        results.Total.Should().BeGreaterThan(0, "Should have at least one test");
        results.Failed.Should().Be(0, $"All tests should pass. {results.Failed} test(s) failed out of {results.Total}");
        results.Passed.Should().Be(results.Total, "All tests should pass");
        results.AllPassed.Should().BeTrue("AllPassed flag should be true");

        // Log results
        TestContext.Progress.WriteLine($"JavaScript Unit Tests Results:");
        TestContext.Progress.WriteLine($"  Total: {results.Total}");
        TestContext.Progress.WriteLine($"  Passed: {results.Passed}");
        TestContext.Progress.WriteLine($"  Failed: {results.Failed}");
    }

    [Test]
    [Description("Verifies getTileKey tests pass")]
    public async Task TileCache_GetTileKey_TestsPass()
    {
        // Arrange
        var testFilePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Tests",
            "tile-cache-unit-tests.html"
        );

        // Act
        await Page.GotoAsync($"file://{testFilePath}");
        await Page.WaitForFunctionAsync("() => window.testResults !== undefined");

        // Check for specific test suite in the HTML
        var getTileKeySection = Page.Locator(".test-suite:has-text('getTileKey()')");
        await Expect(getTileKeySection).ToBeVisibleAsync();

        // Verify no failures in this section
        var failedTests = await getTileKeySection.Locator(".test-fail").CountAsync();
        failedTests.Should().Be(0, "getTileKey() tests should all pass");
    }

    [Test]
    [Description("Verifies latLngToTile tests pass")]
    public async Task TileCache_LatLngToTile_TestsPass()
    {
        // Arrange
        var testFilePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Tests",
            "tile-cache-unit-tests.html"
        );

        // Act
        await Page.GotoAsync($"file://{testFilePath}");
        await Page.WaitForFunctionAsync("() => window.testResults !== undefined");

        // Check for specific test suite
        var latLngSection = Page.Locator(".test-suite:has-text('latLngToTile()')");
        await Expect(latLngSection).ToBeVisibleAsync();

        // Verify no failures
        var failedTests = await latLngSection.Locator(".test-fail").CountAsync();
        failedTests.Should().Be(0, "latLngToTile() tests should all pass");
    }

    [Test]
    [Description("Verifies getTilesForRegion tests pass")]
    public async Task TileCache_GetTilesForRegion_TestsPass()
    {
        // Arrange
        var testFilePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Tests",
            "tile-cache-unit-tests.html"
        );

        // Act
        await Page.GotoAsync($"file://{testFilePath}");
        await Page.WaitForFunctionAsync("() => window.testResults !== undefined");

        // Check for specific test suite
        var regionSection = Page.Locator(".test-suite:has-text('getTilesForRegion()')");
        await Expect(regionSection).ToBeVisibleAsync();

        // Verify no failures
        var failedTests = await regionSection.Locator(".test-fail").CountAsync();
        failedTests.Should().Be(0, "getTilesForRegion() tests should all pass");
    }

    [Test]
    [Description("Verifies estimateRegionSize tests pass")]
    public async Task TileCache_EstimateRegionSize_TestsPass()
    {
        // Arrange
        var testFilePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Tests",
            "tile-cache-unit-tests.html"
        );

        // Act
        await Page.GotoAsync($"file://{testFilePath}");
        await Page.WaitForFunctionAsync("() => window.testResults !== undefined");

        // Check for specific test suite
        var estimateSection = Page.Locator(".test-suite:has-text('estimateRegionSize()')");
        await Expect(estimateSection).ToBeVisibleAsync();

        // Verify no failures
        var failedTests = await estimateSection.Locator(".test-fail").CountAsync();
        failedTests.Should().Be(0, "estimateRegionSize() tests should all pass");
    }

    private class TestResults
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public bool AllPassed { get; set; }
    }
}
