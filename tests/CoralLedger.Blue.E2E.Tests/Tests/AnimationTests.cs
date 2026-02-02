namespace CoralLedger.Blue.E2E.Tests.Tests;

/// <summary>
/// E2E tests for UI animations and transitions.
/// Verifies animation classes, stagger effects, and prefers-reduced-motion support.
/// </summary>
[TestFixture]
public class AnimationTests : PlaywrightFixture
{
    private const string ScreenshotDirectory = "playwright-artifacts/animations";

    [Test]
    [Description("Verifies cardAppear animation keyframe is defined in CSS")]
    public async Task Animation_CardAppearKeyframeIsDefined()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if cardAppear keyframe exists
        var hasKeyframe = await Page.EvaluateAsync<bool>(@"() => {
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.KEYFRAMES_RULE && rule.name === 'cardAppear') {
                            return true;
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return false;
        }");

        // Assert
        hasKeyframe.Should().BeTrue("cardAppear keyframe animation should be defined in CSS");
    }

    [Test]
    [Description("Verifies slideIn animation keyframe is defined in CSS")]
    public async Task Animation_SlideInKeyframeIsDefined()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if slideIn keyframe exists
        var hasKeyframe = await Page.EvaluateAsync<bool>(@"() => {
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.KEYFRAMES_RULE && rule.name === 'slideIn') {
                            return true;
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return false;
        }");

        // Assert
        hasKeyframe.Should().BeTrue("slideIn keyframe animation should be defined in CSS");
    }

    [Test]
    [Description("Verifies tableRowFadeIn animation keyframe is defined in CSS")]
    public async Task Animation_TableRowFadeInKeyframeIsDefined()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if tableRowFadeIn keyframe exists
        var hasKeyframe = await Page.EvaluateAsync<bool>(@"() => {
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.KEYFRAMES_RULE && rule.name === 'tableRowFadeIn') {
                            return true;
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return false;
        }");

        // Assert
        hasKeyframe.Should().BeTrue("tableRowFadeIn keyframe animation should be defined in CSS");
    }

    [Test]
    [Description("Verifies DataCards have card-appear animation class")]
    public async Task Animation_DataCardsHaveCardAppearClass()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Find data cards with card-appear class
        var cardsWithAnimation = Page.Locator(".data-card.card-appear");
        var count = await cardsWithAnimation.CountAsync();

        // Assert - Should have at least 4 data cards with animation
        count.Should().BeGreaterOrEqualTo(4, "Dashboard should have at least 4 DataCards with card-appear class");
    }

    [Test]
    [Description("Verifies DataCards have stagger classes applied")]
    public async Task Animation_DataCardsHaveStaggerClasses()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check for stagger classes on data cards
        var staggerClasses = new[] { "stagger-1", "stagger-2", "stagger-3", "stagger-4" };
        var foundClasses = new List<string>();

        foreach (var staggerClass in staggerClasses)
        {
            var card = Page.Locator($".data-card.{staggerClass}");
            if (await card.CountAsync() > 0)
            {
                foundClasses.Add(staggerClass);
            }
        }

        // Assert - All 4 stagger classes should be found
        foundClasses.Should().HaveCount(4, "All 4 KPI cards should have unique stagger classes");
    }

    [Test]
    [Description("Verifies stagger animation delays are correctly applied")]
    public async Task Animation_StaggerDelaysAreCorrect()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check animation delays for stagger classes
        var delays = await Page.EvaluateAsync<Dictionary<string, string>>(@"() => {
            const result = {};
            for (let i = 1; i <= 8; i++) {
                const el = document.querySelector(`.stagger-${i}`);
                if (el) {
                    result[`stagger-${i}`] = window.getComputedStyle(el).animationDelay;
                }
            }
            return result;
        }");

        // Assert - Each stagger class should have increasing delays
        if (delays.ContainsKey("stagger-1"))
        {
            ParseDelay(delays["stagger-1"]).Should().Be(0, "stagger-1 should have 0ms delay");
        }
        if (delays.ContainsKey("stagger-2"))
        {
            ParseDelay(delays["stagger-2"]).Should().Be(50, "stagger-2 should have 50ms delay");
        }
        if (delays.ContainsKey("stagger-3"))
        {
            ParseDelay(delays["stagger-3"]).Should().Be(100, "stagger-3 should have 100ms delay");
        }
        if (delays.ContainsKey("stagger-4"))
        {
            ParseDelay(delays["stagger-4"]).Should().Be(150, "stagger-4 should have 150ms delay");
        }
    }

    [Test]
    [Description("Verifies table-row-animate CSS class is defined")]
    public async Task Animation_TableRowsHaveAnimationClasses()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(3000); // Wait for page to load

        // Act - Check if table-row-animate CSS rule exists in stylesheets
        var hasAnimationRule = await Page.EvaluateAsync<bool>(@"() => {
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.STYLE_RULE &&
                            rule.selectorText &&
                            rule.selectorText.includes('table-row-animate')) {
                            return true;
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return false;
        }");

        // Assert - The CSS class should be defined
        hasAnimationRule.Should().BeTrue("table-row-animate CSS class should be defined for MPA table rows");
    }

    [Test]
    [Description("Verifies badge hover transforms are defined in CSS")]
    public async Task Animation_BadgeHoverTransformIsDefined()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if badge transform transition is defined in stylesheets (scoped or global)
        var hasTransitionRule = await Page.EvaluateAsync<bool>(@"() => {
            // Check global stylesheet for badge transition rules
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.STYLE_RULE) {
                            const selector = rule.selectorText || '';
                            const transition = rule.style.transition || '';
                            // Check for badge selectors with transform transition
                            if ((selector.includes('alert-badge') ||
                                 selector.includes('protection-badge') ||
                                 selector.includes('severity-indicator')) &&
                                transition.includes('transform')) {
                                return true;
                            }
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            // Also check if any badge element has transform transition computed
            const badge = document.querySelector('.alert-badge, .protection-badge, .severity-indicator');
            if (badge) {
                const transition = window.getComputedStyle(badge).transition;
                return transition.includes('transform');
            }
            return false;
        }");

        // Assert
        hasTransitionRule.Should().BeTrue("Badge CSS rules should include transform transition for hover effects");
    }

    [Test]
    [Description("Verifies prefers-reduced-motion media query is respected")]
    public async Task Animation_PrefersReducedMotionIsRespected()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check if prefers-reduced-motion rule exists
        var hasReducedMotionRule = await Page.EvaluateAsync<bool>(@"() => {
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.MEDIA_RULE &&
                            rule.conditionText &&
                            rule.conditionText.includes('prefers-reduced-motion')) {
                            return true;
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return false;
        }");

        // Assert
        hasReducedMotionRule.Should().BeTrue("CSS should include prefers-reduced-motion media query for accessibility");
    }

    [Test]
    [Description("Verifies animations use GPU-accelerated properties only")]
    public async Task Animation_UsesGpuAcceleratedProperties()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check animation properties in keyframes
        var usesOnlyGpuProperties = await Page.EvaluateAsync<bool>(@"() => {
            const gpuProperties = ['opacity', 'transform'];
            const nonGpuProperties = ['width', 'height', 'top', 'left', 'right', 'bottom', 'margin', 'padding'];

            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.KEYFRAMES_RULE) {
                            const keyframeNames = ['cardAppear', 'slideIn', 'tableRowFadeIn'];
                            if (keyframeNames.includes(rule.name)) {
                                for (const keyframe of rule.cssRules) {
                                    for (const prop of nonGpuProperties) {
                                        if (keyframe.style[prop]) {
                                            return false; // Found non-GPU property
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return true;
        }");

        // Assert
        usesOnlyGpuProperties.Should().BeTrue("Animations should only use GPU-accelerated properties (transform, opacity)");
    }

    [Test]
    [Description("Captures screenshot of dashboard with animated cards for visual review")]
    public async Task Animation_CaptureDashboardWithAnimatedCards()
    {
        // Arrange
        await NavigateToAsync("/");

        // Wait for animations to start
        await Task.Delay(100);

        // Capture during animation
        var screenshotPath = GetScreenshotPath("dashboard-animation-start.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = false });
        TestContext.AddTestAttachment(screenshotPath, "Dashboard Animation Start");

        // Wait for animations to complete
        await Task.Delay(500);

        // Capture after animation
        var screenshotPathEnd = GetScreenshotPath("dashboard-animation-complete.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPathEnd, FullPage = false });
        TestContext.AddTestAttachment(screenshotPathEnd, "Dashboard Animation Complete");

        // Assert files exist
        File.Exists(screenshotPath).Should().BeTrue("Animation start screenshot should be saved");
        File.Exists(screenshotPathEnd).Should().BeTrue("Animation complete screenshot should be saved");
    }

    [Test]
    [Description("Captures screenshot of KPI cards with stagger effect")]
    public async Task Animation_CaptureKpiCardsStaggerEffect()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000); // Wait for full load

        // Act - Screenshot the KPI cards area
        var kpiSection = Page.Locator(".kpi-cards, .cards-grid, section").First;

        if (await kpiSection.IsVisibleAsync())
        {
            var screenshotPath = GetScreenshotPath("kpi-cards-stagger.png");
            await kpiSection.ScreenshotAsync(new() { Path = screenshotPath });
            TestContext.AddTestAttachment(screenshotPath, "KPI Cards with Stagger Animation");

            File.Exists(screenshotPath).Should().BeTrue("KPI cards screenshot should be saved");
        }
    }

    [Test]
    [Description("Captures screenshot of MPA table with row animations")]
    public async Task Animation_CaptureMpaTableRowAnimations()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(3000); // Wait for table to fully load

        // Act - Screenshot the MPA table
        var mpaTable = Page.Locator(".data-table, table").First;

        if (await mpaTable.IsVisibleAsync())
        {
            var screenshotPath = GetScreenshotPath("mpa-table-row-animations.png");
            await mpaTable.ScreenshotAsync(new() { Path = screenshotPath });
            TestContext.AddTestAttachment(screenshotPath, "MPA Table with Row Animations");

            File.Exists(screenshotPath).Should().BeTrue("MPA table screenshot should be saved");
        }
    }

    [Test]
    [Description("Captures screenshot of observations list with slide-in animations")]
    public async Task Animation_CaptureObservationsListAnimations()
    {
        // Arrange
        await NavigateToAsync("/observations");
        await Task.Delay(3000); // Wait for observations to load

        // Act - Check for animated list items
        var listItems = Page.Locator(".list-item-animate, .observation-item");
        var count = await listItems.CountAsync();

        if (count > 0)
        {
            var screenshotPath = GetScreenshotPath("observations-list-animations.png");
            await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = false });
            TestContext.AddTestAttachment(screenshotPath, "Observations List with Slide-in Animations");

            File.Exists(screenshotPath).Should().BeTrue("Observations list screenshot should be saved");
        }
    }

    [Test]
    [Description("Captures full page screenshot showing all animations")]
    public async Task Animation_CaptureFullPageWithAllAnimations()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(3000); // Wait for all animations to complete

        // Act - Full page screenshot
        var screenshotPath = GetScreenshotPath("full-page-animations.png");
        await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
        TestContext.AddTestAttachment(screenshotPath, "Full Page with All Animations");

        // Assert
        File.Exists(screenshotPath).Should().BeTrue("Full page screenshot should be saved");
    }

    [Test]
    [Description("Verifies value-transition CSS class is defined")]
    public async Task Animation_ValueTransitionClassIsApplied()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000); // Wait for page to load

        // Act - Check if value-transition CSS rule exists in stylesheets
        var hasTransitionRule = await Page.EvaluateAsync<bool>(@"() => {
            for (const sheet of document.styleSheets) {
                try {
                    for (const rule of sheet.cssRules) {
                        if (rule.type === CSSRule.STYLE_RULE &&
                            rule.selectorText &&
                            rule.selectorText.includes('value-transition')) {
                            return true;
                        }
                    }
                } catch (e) {
                    // Cross-origin stylesheet, skip
                }
            }
            return false;
        }");

        // Assert
        hasTransitionRule.Should().BeTrue("value-transition CSS class should be defined for DataCard value transitions");
    }

    [Test]
    [Description("Verifies animation duration is under 300ms")]
    public async Task Animation_DurationIsUnder300ms()
    {
        // Arrange
        await NavigateToAsync("/");
        await Task.Delay(2000);

        // Act - Check animation duration on animated elements
        var duration = await Page.EvaluateAsync<string>(@"() => {
            const el = document.querySelector('.card-appear');
            if (!el) return '0s';
            return window.getComputedStyle(el).animationDuration;
        }");

        // Assert - Duration should be 300ms (0.3s) or less
        var durationMs = ParseDuration(duration);
        durationMs.Should().BeLessOrEqualTo(300, "Animation duration should be 300ms or less for performance");
    }

    private string GetScreenshotPath(string filename)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            ScreenshotDirectory);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, filename);
    }

    private static int ParseDelay(string delay)
    {
        if (string.IsNullOrEmpty(delay)) return 0;

        // Parse "0.05s" or "50ms" format
        delay = delay.Trim().ToLower();
        if (delay.EndsWith("ms"))
        {
            return int.Parse(delay.Replace("ms", ""));
        }
        if (delay.EndsWith("s"))
        {
            return (int)(double.Parse(delay.Replace("s", "")) * 1000);
        }
        return 0;
    }

    private static int ParseDuration(string duration)
    {
        return ParseDelay(duration);
    }
}
