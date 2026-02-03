using CoralLedger.Blue.Web.Services;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ToastService - verifies toast notification event handling
/// </summary>
public class ToastServiceTests
{
    private readonly ToastService _service;

    public ToastServiceTests()
    {
        _service = new ToastService();
    }

    [Fact]
    public void ShowSuccess_RaisesEventWithCorrectType()
    {
        // Arrange
        ToastEventArgs? capturedArgs = null;
        _service.OnToastShown += (sender, args) => capturedArgs = args;

        // Act
        _service.ShowSuccess("Success message", "Success Title");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Type.Should().Be(ToastType.Success);
        capturedArgs.Message.Should().Be("Success message");
        capturedArgs.Title.Should().Be("Success Title");
        capturedArgs.Id.Should().NotBeEmpty();
        capturedArgs.AutoCloseDuration.Should().Be(5000);
    }

    [Fact]
    public void ShowError_RaisesEventWithCorrectType()
    {
        // Arrange
        ToastEventArgs? capturedArgs = null;
        _service.OnToastShown += (sender, args) => capturedArgs = args;

        // Act
        _service.ShowError("Error message", "Error Title");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Type.Should().Be(ToastType.Error);
        capturedArgs.Message.Should().Be("Error message");
        capturedArgs.Title.Should().Be("Error Title");
    }

    [Fact]
    public void ShowWarning_RaisesEventWithCorrectType()
    {
        // Arrange
        ToastEventArgs? capturedArgs = null;
        _service.OnToastShown += (sender, args) => capturedArgs = args;

        // Act
        _service.ShowWarning("Warning message", "Warning Title");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Type.Should().Be(ToastType.Warning);
        capturedArgs.Message.Should().Be("Warning message");
        capturedArgs.Title.Should().Be("Warning Title");
    }

    [Fact]
    public void ShowInfo_RaisesEventWithCorrectType()
    {
        // Arrange
        ToastEventArgs? capturedArgs = null;
        _service.OnToastShown += (sender, args) => capturedArgs = args;

        // Act
        _service.ShowInfo("Info message", "Info Title");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Type.Should().Be(ToastType.Info);
        capturedArgs.Message.Should().Be("Info message");
        capturedArgs.Title.Should().Be("Info Title");
    }

    [Fact]
    public void ShowSuccess_WithCustomDuration_UsesCustomDuration()
    {
        // Arrange
        ToastEventArgs? capturedArgs = null;
        _service.OnToastShown += (sender, args) => capturedArgs = args;

        // Act
        _service.ShowSuccess("Message", "Title", 3000);

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.AutoCloseDuration.Should().Be(3000);
    }

    [Fact]
    public void ShowSuccess_WithNullTitle_AllowsNullTitle()
    {
        // Arrange
        ToastEventArgs? capturedArgs = null;
        _service.OnToastShown += (sender, args) => capturedArgs = args;

        // Act
        _service.ShowSuccess("Message only");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Title.Should().BeNull();
        capturedArgs.Message.Should().Be("Message only");
    }

    [Fact]
    public void MultipleToasts_EachRaisesEvent()
    {
        // Arrange
        var capturedEvents = new List<ToastEventArgs>();
        _service.OnToastShown += (sender, args) => capturedEvents.Add(args);

        // Act
        _service.ShowSuccess("Success");
        _service.ShowError("Error");
        _service.ShowWarning("Warning");

        // Assert
        capturedEvents.Should().HaveCount(3);
        capturedEvents[0].Type.Should().Be(ToastType.Success);
        capturedEvents[1].Type.Should().Be(ToastType.Error);
        capturedEvents[2].Type.Should().Be(ToastType.Warning);
    }

    [Fact]
    public void NoSubscribers_DoesNotThrow()
    {
        // Act
        Action act = () => _service.ShowSuccess("Message");

        // Assert
        act.Should().NotThrow();
    }
}
