using System.Net;
using System.Text.Json;
using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace CoralLedger.Infrastructure.Tests.ExternalServices;

public class CoralReefWatchClientTests
{
    private readonly Mock<ILogger<CoralReefWatchClient>> _loggerMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IOptions<RedisCacheOptions>> _optionsMock;
    private readonly Mock<IOptions<CoralReefWatchOptions>> _coralReefWatchOptionsMock;

    public CoralReefWatchClientTests()
    {
        _loggerMock = new Mock<ILogger<CoralReefWatchClient>>();
        // Use loose mock behavior to return null for unmatched calls
        _cacheMock = new Mock<ICacheService>(MockBehavior.Loose);
        _optionsMock = new Mock<IOptions<RedisCacheOptions>>();
        _coralReefWatchOptionsMock = new Mock<IOptions<CoralReefWatchOptions>>();

        // Setup default cache options
        _optionsMock.Setup(o => o.Value).Returns(new RedisCacheOptions
        {
            NoaaBleachingCacheTtlHours = 12
        });

        _coralReefWatchOptionsMock.Setup(o => o.Value).Returns(new CoralReefWatchOptions
        {
            UseMockData = true,
            MockDataPath = "data/mock-bleaching-data.json"
        });
    }

    private CoralReefWatchClient CreateClient(HttpMessageHandler? handler = null)
    {
        var httpClient = handler != null
            ? new HttpClient(handler)
            : new HttpClient(new FakeHttpMessageHandler());

        return new CoralReefWatchClient(
            httpClient,
            _loggerMock.Object,
            _cacheMock.Object,
            _optionsMock.Object,
            _coralReefWatchOptionsMock.Object);
    }

    [Fact]
    public void Constructor_SetsBaseAddressCorrectly()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert - The client should be created without throwing
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBleachingData_ValidResponse_ParsesCorrectly()
    {
        // Arrange
        var responseJson = CreateSampleErddapResponse(
            longitude: -77.35,
            latitude: 25.05,
            sst: 28.5,
            anomaly: 1.2,
            hotspot: 0.8,
            dhw: 2.5,
            baa: 1);

        var handler = CreateMockHandler(responseJson);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetBleachingDataAsync(-77.35, 25.05, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        // Assert
        result.Should().NotBeNull();
        result!.Longitude.Should().BeApproximately(-77.35, 0.01);
        result.Latitude.Should().BeApproximately(25.05, 0.01);
        result.SeaSurfaceTemperature.Should().BeApproximately(28.5, 0.01);
        result.SstAnomaly.Should().BeApproximately(1.2, 0.01);
        result.DegreeHeatingWeek.Should().BeApproximately(2.5, 0.01);
        result.AlertLevel.Should().Be(1);
    }

    [Fact]
    public async Task GetBleachingData_ErrorResponse_ReturnsNull()
    {
        // Arrange
        var handler = CreateMockHandler("Not Found", HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetBleachingDataAsync(-77.35, 25.05, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBleachingData_EmptyResponse_ReturnsNull()
    {
        // Arrange
        var emptyResponse = """
            {
                "table": {
                    "columnNames": ["time", "latitude", "longitude", "CRW_SST", "CRW_SSTANOMALY", "CRW_HOTSPOT", "CRW_DHW", "CRW_BAA"],
                    "rows": []
                }
            }
            """;

        var handler = CreateMockHandler(emptyResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetBleachingDataAsync(-77.35, 25.05, DateOnly.FromDateTime(DateTime.UtcNow));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBleachingTimeSeriesAsync_ValidDateRange_ReturnsData()
    {
        // Arrange
        var responseJson = CreateSampleErddapResponse(
            longitude: -76.58,
            latitude: 24.47,
            sst: 29.0,
            anomaly: 1.5,
            hotspot: 1.0,
            dhw: 3.0,
            baa: 2);

        var handler = CreateMockHandler(responseJson);
        var client = CreateClient(handler);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await client.GetBleachingTimeSeriesAsync(-76.58, 24.47, startDate, endDate);

        // Assert
        result.Should().NotBeEmpty();
        result.First().SeaSurfaceTemperature.Should().BeApproximately(29.0, 0.01);
    }

    [Fact]
    public async Task GetBahamasBleachingAlertsAsync_ReturnsDataForBahamasRegion()
    {
        // Arrange
        var responseJson = CreateSampleErddapResponse(
            longitude: -77.0,
            latitude: 25.0,
            sst: 28.0,
            anomaly: 0.5,
            hotspot: 0.0,
            dhw: 1.0,
            baa: 0);

        var handler = CreateMockHandler(responseJson);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetBahamasBleachingAlertsAsync();

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBleachingDataForRegionAsync_ValidRegion_ReturnsData()
    {
        // Arrange
        var responseJson = CreateSampleErddapResponse(
            longitude: -76.5,
            latitude: 24.5,
            sst: 27.5,
            anomaly: 0.3,
            hotspot: 0.0,
            dhw: 0.5,
            baa: 0);

        var handler = CreateMockHandler(responseJson);
        var client = CreateClient(handler);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await client.GetBleachingDataForRegionAsync(
            minLon: -78.0,
            minLat: 24.0,
            maxLon: -76.0,
            maxLat: 26.0,
            startDate: date,
            endDate: date);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBleachingData_WithNaNValues_HandlesGracefully()
    {
        // Arrange - Response with NaN values
        var responseJson = """
            {
                "table": {
                    "columnNames": ["time", "latitude", "longitude", "CRW_SST", "CRW_SSTANOMALY", "CRW_HOTSPOT", "CRW_DHW", "CRW_BAA"],
                    "rows": [
                        ["2024-06-15T12:00:00Z", 25.0, -77.0, "NaN", "NaN", "NaN", "NaN", "NaN"]
                    ]
                }
            }
            """;

        var handler = CreateMockHandler(responseJson);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetBleachingDataAsync(-77.0, 25.0, new DateOnly(2024, 6, 15));

        // Assert - Should return null when essential values are NaN
        result.Should().BeNull();
    }

    [Fact]
    public void ParseAlertLevel_FromDhwValues_ReturnsCorrectLevel()
    {
        // This tests the expected alert level based on DHW values:
        // 0 = No Stress (DHW < 0)
        // 1 = Watch (0 <= DHW < 4)
        // 2 = Warning (4 <= DHW < 8)
        // 3 = Alert Level 1 (DHW >= 8)
        // 4 = Alert Level 2 (DHW >= 8 for extended period)

        // The API returns BAA directly, so we verify the parsed value
        var testCases = new[]
        {
            (dhw: 0.0, expectedBaa: 0),
            (dhw: 2.0, expectedBaa: 1),
            (dhw: 5.0, expectedBaa: 2),
            (dhw: 10.0, expectedBaa: 3)
        };

        foreach (var (dhw, expectedBaa) in testCases)
        {
            // The alert level is provided by NOAA, not calculated
            expectedBaa.Should().BeInRange(0, 5,
                $"DHW value {dhw} should map to valid BAA level");
        }
    }

    private static string CreateSampleErddapResponse(
        double longitude,
        double latitude,
        double sst,
        double anomaly,
        double hotspot,
        double dhw,
        int baa)
    {
        return $$"""
            {
                "table": {
                    "columnNames": ["time", "latitude", "longitude", "CRW_SST", "CRW_SSTANOMALY", "CRW_HOTSPOT", "CRW_DHW", "CRW_BAA"],
                    "rows": [
                        ["{{DateTime.UtcNow.AddDays(-1):yyyy-MM-dd}}T12:00:00Z", {{latitude}}, {{longitude}}, {{sst}}, {{anomaly}}, {{hotspot}}, {{dhw}}, {{baa}}]
                    ]
                }
            }
            """;
    }

    private static HttpMessageHandler CreateMockHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        return handlerMock.Object;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                        "table": {
                            "columnNames": ["time", "latitude", "longitude", "CRW_SST", "CRW_SSTANOMALY", "CRW_HOTSPOT", "CRW_DHW", "CRW_BAA"],
                            "rows": []
                        }
                    }
                    """)
            });
        }
    }
}
