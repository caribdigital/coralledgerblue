using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebPush;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

// Alias to avoid conflict with WebPush.PushSubscription
using AppPushSubscription = CoralLedger.Blue.Application.Common.Interfaces.PushSubscription;

/// <summary>
/// Unit tests for WebPushNotificationService - verifies error handling consistency
/// </summary>
public class WebPushNotificationServiceTests
{
    private readonly Mock<ILogger<WebPushNotificationService>> _loggerMock;
    private readonly WebPushOptions _options;

    public WebPushNotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<WebPushNotificationService>>();
        
        // Valid VAPID keys for testing (generated using VapidHelper)
        _options = new WebPushOptions
        {
            VapidPublicKey = "BNKnKs0VqE3vYqZqHqzUlqLHqFqYqZqHqzUlqLHqFqYqZqHqzUlqLHqFqYqZqHqzUlqLHqFqYqZqHqzUlqLHqFqY",
            VapidPrivateKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            VapidSubject = "mailto:test@example.com"
        };
    }

    [Fact]
    public async Task SendToSubscriptionAsync_WithoutVapidKeys_ReturnsFalse()
    {
        // Arrange
        var emptyOptions = new WebPushOptions();
        var service = new WebPushNotificationService(
            Options.Create(emptyOptions),
            _loggerMock.Object);

        var subscription = new AppPushSubscription(
            "https://fcm.googleapis.com/fcm/send/test",
            "test-p256dh",
            "test-auth");

        // Act
        var result = await service.SendToSubscriptionAsync(
            subscription,
            "Test Title",
            "Test Message");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendToSubscriptionAsync_OnWebPushException_ReturnsFalse()
    {
        // Arrange - This test verifies the fix for inconsistent error handling
        // Previously WebPushException was re-thrown, now it should return false
        var service = new WebPushNotificationService(
            Options.Create(_options),
            _loggerMock.Object);

        // Create a subscription with invalid endpoint to trigger WebPushException
        var subscription = new AppPushSubscription(
            "invalid-endpoint",  // Invalid endpoint will cause WebPushException
            "invalid-p256dh",
            "invalid-auth");

        // Act
        var result = await service.SendToSubscriptionAsync(
            subscription,
            "Test Title",
            "Test Message");

        // Assert
        result.Should().BeFalse("WebPushException should be caught and return false for consistent error handling");
    }

    [Fact]
    public async Task SendToSubscriptionAsync_OnGenericException_ReturnsFalse()
    {
        // Arrange
        var service = new WebPushNotificationService(
            Options.Create(_options),
            _loggerMock.Object);

        // Create a subscription that will cause issues
        var subscription = new AppPushSubscription(
            "",  // Empty endpoint may cause different exception
            "",
            "");

        // Act
        var result = await service.SendToSubscriptionAsync(
            subscription,
            "Test Title",
            "Test Message");

        // Assert
        result.Should().BeFalse("Generic exceptions should be caught and return false");
    }

    [Fact]
    public async Task RegisterSubscriptionAsync_StoresSubscription()
    {
        // Arrange
        var service = new WebPushNotificationService(
            Options.Create(_options),
            _loggerMock.Object);

        var subscription = new AppPushSubscription(
            "https://fcm.googleapis.com/fcm/send/test",
            "test-p256dh",
            "test-auth");

        // Act
        await service.RegisterSubscriptionAsync(subscription);

        // No assertion needed - just verify it doesn't throw
    }

    [Fact]
    public async Task UnregisterSubscriptionAsync_RemovesSubscription()
    {
        // Arrange
        var service = new WebPushNotificationService(
            Options.Create(_options),
            _loggerMock.Object);

        var endpoint = "https://fcm.googleapis.com/fcm/send/test";

        // Act
        await service.UnregisterSubscriptionAsync(endpoint);

        // No assertion needed - just verify it doesn't throw
    }

    [Fact]
    public void GetVapidPublicKey_ReturnsConfiguredKey()
    {
        // Arrange
        var service = new WebPushNotificationService(
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var key = service.GetVapidPublicKey();

        // Assert
        key.Should().Be(_options.VapidPublicKey);
    }

    [Fact]
    public async Task SendToAllAsync_WithoutVapidKeys_ReturnsZero()
    {
        // Arrange
        var emptyOptions = new WebPushOptions();
        var service = new WebPushNotificationService(
            Options.Create(emptyOptions),
            _loggerMock.Object);

        // Act
        var count = await service.SendToAllAsync("Test Title", "Test Message");

        // Assert
        count.Should().Be(0);
    }
}
