using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace CoralLedger.Blue.E2E.Tests;

/// <summary>
/// Base test fixture for Playwright E2E tests.
/// Provides browser context and page management with configuration.
/// </summary>
public class PlaywrightFixture : PageTest
{
    private static readonly FieldInfo PageField = typeof(PageTest).GetField("<Page>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void ReplacePage(PageTest instance, IPage page)
    {
        PageField.SetValue(instance, page);
    }

    private IBrowserContext? _secureContext;

    protected string BaseUrl { get; private set; } = null!;
    protected IConfiguration Configuration { get; private set; } = null!;
    protected List<string> ConsoleErrors { get; } = new();

    [SetUp]
    public async Task BaseSetUp()
    {
        await EnsureSecureContextAsync();

        // Load configuration
        Configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.e2e.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get base URL from environment or config
        var environmentUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        var configuredUrl = environmentUrl ?? Configuration["BaseUrl"];
        var alternateUrl = Configuration["AlternateBaseUrl"];

        BaseUrl = await ResolveBaseUrlAsync(configuredUrl, alternateUrl);

        // Track console errors
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                ConsoleErrors.Add($"[{msg.Type}] {msg.Text}");
            }
        };

        // Configure default timeout
        var timeout = int.Parse(Configuration["Timeout"] ?? "30000");
        Page.SetDefaultTimeout(timeout);
    }

    [TearDown]
    public async Task BaseTearDown()
    {
        // Take screenshot on failure
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var screenshotPath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "playwright-artifacts",
                $"{TestContext.CurrentContext.Test.Name}-failure.png");

            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
            await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
            TestContext.AddTestAttachment(screenshotPath, "Failure Screenshot");
        }

        // Close secure context
        if (_secureContext != null)
        {
            await _secureContext.CloseAsync();
            _secureContext = null;
        }

        // Clear console errors for next test
        ConsoleErrors.Clear();
    }

    protected async Task WaitForBlazorAsync()
    {
        // Wait for Blazor to initialize - check for Blazor object or SignalR connection
        // In .NET 8+ Blazor Server, window.Blazor may not always be exposed
        try
        {
            await Page.WaitForFunctionAsync(
                "() => window.Blazor !== undefined || document.querySelector('[blazor-component-id]') !== null || document.readyState === 'complete'",
                new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            // Fallback: just wait for DOM to be ready
            TestContext.Progress.WriteLine("Blazor detection timed out, falling back to DOM ready check");
        }
        // Give a brief moment for initial render
        await Task.Delay(1000);
    }

    protected async Task NavigateToAsync(string path)
    {
        await Page.GotoAsync($"{BaseUrl}{path}", new()
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        await WaitForBlazorAsync();
    }

    protected void AssertNoConsoleErrors()
    {
        ConsoleErrors.Should().BeEmpty("Page should not have console errors");
    }

    private async Task EnsureSecureContextAsync()
    {
        if (Page != null)
        {
            if (Page.Context != null)
            {
                await Page.Context.CloseAsync();
            }

            await Page.CloseAsync();
        }

        _secureContext = await Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true
        });
        var newPage = await _secureContext.NewPageAsync();
        ReplacePage(this, newPage);
    }
    
    private async Task<string> ResolveBaseUrlAsync(string? primary, string? alternate)
    {
        // Only target the web app, never the Aspire dashboard (17088)
        // The Aspire dashboard has a strict CSP that blocks Playwright tests
        var candidates = new List<string?>
        {
            primary,
            alternate,
            "https://localhost:7232",
            "http://localhost:5147"
        };

        // Retry with longer timeout to allow app to fully start
        for (int retry = 0; retry < 3; retry++)
        {
            foreach (var candidate in candidates.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await IsUrlResponsiveAsync(candidate!))
                {
                    TestContext.Progress.WriteLine($"Playwright targeting {candidate}");
                    return candidate!;
                }
            }

            if (retry < 2)
            {
                TestContext.Progress.WriteLine($"No responsive URL found, retrying in 5 seconds... (attempt {retry + 1}/3)");
                await Task.Delay(5000);
            }
        }

        var fallback = candidates.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)) ?? "https://localhost:7232";
        TestContext.Progress.WriteLine($"Playwright fallback base URL {fallback}");
        return fallback;
    }

    private static async Task<bool> IsUrlResponsiveAsync(string url)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                AllowAutoRedirect = false // Don't follow redirects, just check if server responds
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // Consider success (2xx) and redirects (3xx) as responsive
            var statusCode = (int)response.StatusCode;
            return statusCode >= 200 && statusCode < 400;
        }
        catch
        {
            return false;
        }
    }
}
