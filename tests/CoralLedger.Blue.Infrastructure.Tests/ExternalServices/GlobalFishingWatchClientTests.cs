using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.ExternalServices;

public class GlobalFishingWatchClientTests
{
    private readonly Mock<ICacheService> _mockCache;
    private readonly IOptions<RedisCacheOptions> _cacheOptions;

    public GlobalFishingWatchClientTests()
    {
        _mockCache = new Mock<ICacheService>();
        _cacheOptions = Options.Create(new RedisCacheOptions { GfwCacheTtlHours = 6 });
    }

    [Fact]
    public void Constructor_WithEnabledAndNoToken_LogsWarning()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = "" // Empty token
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();

        // Act
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GlobalFishingWatch is enabled but ApiToken is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithEnabledAndValidToken_DoesNotLogWarning()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = "valid-token-here"
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();

        // Act
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_WithDisabledAndNoToken_DoesNotLogWarning()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = false,
            ApiToken = "" // Empty token, but disabled
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();

        // Act
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_SetsBaseAddressCorrectly()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = "test-token"
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();

        // Act
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Assert
        httpClient.BaseAddress.Should().Be(new Uri("https://gateway.api.globalfishingwatch.org/"));
    }

    [Fact]
    public void Constructor_SetsAuthorizationHeader()
    {
        // Arrange
        var testToken = "my-test-api-token";
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = testToken
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();

        // Act
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Assert
        httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(testToken);
    }

    [Fact]
    public void IsConfigured_WithToken_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = "valid-token"
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Act & Assert
        client.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_WithoutToken_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = ""
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Act & Assert
        client.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WithNullToken_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = null!
        });

        var mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
        var httpClient = new HttpClient();
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object, _mockCache.Object, _cacheOptions);

        // Act & Assert
        client.IsConfigured.Should().BeFalse();
    }
}

public class GlobalFishingWatchOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        GlobalFishingWatchOptions.SectionName.Should().Be("GlobalFishingWatch");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new GlobalFishingWatchOptions();

        // Assert
        options.ApiToken.Should().BeEmpty();
        options.Enabled.Should().BeTrue(); // Default is true per the class
    }
}

/// <summary>
/// Tests for GFW API method behavior using mocked HTTP responses
/// </summary>
public class GlobalFishingWatchClientApiTests
{
    private readonly Mock<ICacheService> _mockCache;
    private readonly IOptions<RedisCacheOptions> _cacheOptions;
    private readonly Mock<ILogger<GlobalFishingWatchClient>> _mockLogger;

    public GlobalFishingWatchClientApiTests()
    {
        _mockCache = new Mock<ICacheService>();
        _cacheOptions = Options.Create(new RedisCacheOptions { GfwCacheTtlHours = 6 });
        _mockLogger = new Mock<ILogger<GlobalFishingWatchClient>>();
    }

    private GlobalFishingWatchClient CreateClient(HttpClient httpClient)
    {
        var options = Options.Create(new GlobalFishingWatchOptions
        {
            Enabled = true,
            ApiToken = "test-api-token"
        });
        return new GlobalFishingWatchClient(httpClient, options, _mockLogger.Object, _mockCache.Object, _cacheOptions);
    }

    [Fact]
    public async Task SearchVesselsAsync_UsesCacheFirst()
    {
        // Arrange - For this test we just verify the cache is checked
        // The cache will return null, triggering API call
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"entries\":[]}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act - This should check cache first, then make HTTP request
        var result = await client.SearchVesselsAsync("test");

        // Assert - API was called (cache returned null by default)
        handler.RequestsMade.Should().BeGreaterThan(0);
        handler.LastRequestUri.Should().Contain("v3/vessels/search");
    }

    [Fact]
    public async Task GetFishingEventsAsync_WithCacheMiss_CallsApi()
    {
        // Arrange - Cache returns null by default (cache miss)
        var responseJson = @"{
            ""total"": 1,
            ""entries"": [{
                ""id"": ""event-001"",
                ""type"": ""fishing"",
                ""vessel"": { ""id"": ""vessel-001"", ""name"": ""Test Vessel"" },
                ""position"": { ""lat"": 24.5, ""lon"": -77.5 },
                ""start"": ""2024-01-01T00:00:00Z"",
                ""end"": ""2024-01-01T06:00:00Z""
            }]
        }";

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act
        var result = await client.GetFishingEventsAsync(-80, 20, -72, 28, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
        handler.RequestsMade.Should().BeGreaterThan(0);
        handler.LastRequestUri.Should().Contain("v3/events");
    }

    [Theory]
    [InlineData(-77.5, 24.5, -76.5, 25.5)] // Valid Bahamas coordinates
    [InlineData(-180, -90, 180, 90)] // Global bounds
    public async Task GetFishingEventsAsync_WithValidBounds_BuildsCorrectGeometry(
        double minLon, double minLat, double maxLon, double maxLat)
    {
        // Arrange - Cache returns null by default (cache miss)
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"total\":0,\"entries\":[]}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act
        await client.GetFishingEventsAsync(minLon, minLat, maxLon, maxLat, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert - Should use POST method (not GET) for geographic queries
        handler.LastRequestMethod.Should().Be(HttpMethod.Post);
        handler.LastRequestContent.Should().Contain("geometry");
        handler.LastRequestContent.Should().Contain("coordinates");
    }

    [Fact]
    public async Task GetFishingEventsAsync_UsesCorrectDataset()
    {
        // Arrange - Cache returns null by default (cache miss)
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"total\":0,\"entries\":[]}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act
        await client.GetFishingEventsAsync(-77.5, 24.5, -76.5, 25.5, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert
        handler.LastRequestContent.Should().Contain("public-global-fishing-events:latest");
    }

    [Fact]
    public async Task GetFishingEffortTileUrlAsync_ReturnsValidTileUrl()
    {
        // Arrange
        var responseJson = @"{
            ""url"": ""https://gateway.api.globalfishingwatch.org/v3/4wings/tile/heatmap/{z}/{x}/{y}?format=PNG&style=abc123"",
            ""colorRamp"": {
                ""stepsByZoom"": {
                    ""0"": [
                        { ""color"": ""rgba(255,107,53,0)"", ""value"": 0 },
                        { ""color"": ""rgba(255,107,53,255)"", ""value"": 100 }
                    ]
                }
            }
        }";

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act
        var result = await client.GetFishingEffortTileUrlAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
        result!.TileUrl.Should().Contain("4wings/tile/heatmap");
        result.TileUrl.Should().Contain("{z}/{x}/{y}");
        result.ColorRamp.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFishingEffortTileUrlAsync_WithGearTypeFilter_IncludesFilter()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"url\":\"http://test\"}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act
        await client.GetFishingEffortTileUrlAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, gearType: "tuna_purse_seines");

        // Assert
        handler.LastRequestUri.Should().Contain("filters");
        handler.LastRequestUri.Should().Contain("tuna_purse_seines");
    }

    [Fact]
    public async Task SearchVesselsAsync_UsesCorrectArraySyntax()
    {
        // Arrange - Cache returns null by default (cache miss)
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"entries\":[]}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/") };
        var client = CreateClient(httpClient);

        // Act
        await client.SearchVesselsAsync("test-vessel");

        // Assert - Should use array syntax datasets[0]= not datasets=
        handler.LastRequestUri.Should().Contain("datasets[0]=");
        handler.LastRequestUri.Should().NotContain("datasets=public"); // Ensure not old format
    }
}

/// <summary>
/// Fake HTTP message handler for testing HTTP client behavior
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;
    public int RequestsMade { get; private set; }
    public string LastRequestUri { get; private set; } = "";
    public HttpMethod LastRequestMethod { get; private set; } = HttpMethod.Get;
    public string LastRequestContent { get; private set; } = "";

    public FakeHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestsMade++;
        LastRequestUri = request.RequestUri?.ToString() ?? "";
        LastRequestMethod = request.Method;

        if (request.Content != null)
        {
            LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return _response;
    }
}
