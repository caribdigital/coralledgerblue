using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace CoralLedger.Aspire.Tests;

/// <summary>
/// xUnit fixture that starts the full Aspire distributed application for integration testing.
/// Provides access to the web application's HTTP client and other resources.
/// </summary>
public class AspireIntegrationFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _webClient;
    private string? _webBaseUrl;

    public HttpClient WebClient => _webClient ?? throw new InvalidOperationException("Fixture not initialized");
    public string WebBaseUrl => _webBaseUrl ?? throw new InvalidOperationException("Fixture not initialized");
    public DistributedApplication App => _app ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        // Create the Aspire application builder
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CoralLedger_AppHost>();

        // Build and start the application
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get the HTTP client for the web resource
        _webClient = _app.CreateHttpClient("web");

        // Get the base URL from the endpoint using the correct API
        _webBaseUrl = _app.GetEndpoint("web", "https")?.AbsoluteUri.TrimEnd('/')
            ?? "https://localhost:7232";

        // Wait for application to be fully ready using our own readiness polling
        await WaitForReadinessAsync();
    }

    private async Task WaitForReadinessAsync()
    {
        var maxAttempts = 60;
        var delay = TimeSpan.FromSeconds(2);

        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var response = await _webClient!.GetAsync("/api/diagnostics/ready");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignore and retry
            }

            await Task.Delay(delay);
        }

        throw new TimeoutException("Application did not become ready within the expected time");
    }

    public async Task DisposeAsync()
    {
        _webClient?.Dispose();

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}

/// <summary>
/// Collection definition for sharing the Aspire fixture across tests
/// </summary>
[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireIntegrationFixture>
{
}
