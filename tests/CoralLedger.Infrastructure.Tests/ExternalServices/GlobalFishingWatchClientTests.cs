using CoralLedger.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoralLedger.Infrastructure.Tests.ExternalServices;

public class GlobalFishingWatchClientTests
{
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
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object);

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
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object);

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
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object);

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
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object);

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
        var client = new GlobalFishingWatchClient(httpClient, options, mockLogger.Object);

        // Assert
        httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(testToken);
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
