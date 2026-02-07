using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Gamification;
using CoralLedger.Blue.Application.Features.Gamification.EventHandlers;
using CoralLedger.Blue.Application.Tests.TestFixtures;
using CoralLedger.Blue.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoralLedger.Blue.Application.Tests.Features.Gamification.EventHandlers;

public class ObservationRejectedEventHandlerTests
{
    private readonly Mock<IMarineDbContext> _contextMock;
    private readonly Mock<ILogger<ObservationRejectedEventHandler>> _loggerMock;
    private readonly ObservationRejectedEventHandler _handler;

    public ObservationRejectedEventHandlerTests()
    {
        _contextMock = new Mock<IMarineDbContext>();
        _loggerMock = new Mock<ILogger<ObservationRejectedEventHandler>>();
        _handler = new ObservationRejectedEventHandler(_contextMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingUserPoints_DeductsPoints()
    {
        // Arrange
        var citizenEmail = "test@example.com";
        var rejectedEvent = new ObservationRejectedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            Reason: "Invalid observation"
        );

        var userPoints = UserPoints.Create(citizenEmail);
        userPoints.AddPoints(100); // Give some initial points

        var userPointsList = new List<UserPoints> { userPoints };
        SetupMockDbSet(userPointsList);

        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(rejectedEvent, CancellationToken.None);

        // Assert
        userPoints.TotalPoints.Should().Be(100 - GamificationConstants.RejectionPenaltyPoints);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoPoints_DoesNotThrow()
    {
        // Arrange
        var citizenEmail = "nopoints@example.com";
        var rejectedEvent = new ObservationRejectedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            Reason: "Invalid observation"
        );

        var emptyList = new List<UserPoints>();
        SetupMockDbSet(emptyList);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            _handler.Handle(rejectedEvent, CancellationToken.None));

        // Assert
        exception.Should().BeNull();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserHasMinimalPoints_DoesNotGoNegative()
    {
        // Arrange
        var citizenEmail = "lowpoints@example.com";
        var rejectedEvent = new ObservationRejectedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            Reason: "Invalid observation"
        );

        var userPoints = UserPoints.Create(citizenEmail);
        userPoints.AddPoints(2); // Less than the penalty

        var userPointsList = new List<UserPoints> { userPoints };
        SetupMockDbSet(userPointsList);

        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(rejectedEvent, CancellationToken.None);

        // Assert
        userPoints.TotalPoints.Should().Be(0); // Should floor at 0, not go negative
    }

    [Fact]
    public async Task Handle_LogsInformationOnSuccess()
    {
        // Arrange
        var citizenEmail = "test@example.com";
        var observationId = Guid.NewGuid();
        var rejectedEvent = new ObservationRejectedEvent(
            ObservationId: observationId,
            CitizenEmail: citizenEmail,
            Reason: "Invalid observation"
        );

        var userPoints = UserPoints.Create(citizenEmail);
        userPoints.AddPoints(100);

        var userPointsList = new List<UserPoints> { userPoints };
        SetupMockDbSet(userPointsList);

        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(rejectedEvent, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deducted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDbThrows_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        var citizenEmail = "test@example.com";
        var rejectedEvent = new ObservationRejectedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            Reason: "Invalid observation"
        );

        var userPoints = UserPoints.Create(citizenEmail);
        userPoints.AddPoints(100);

        var userPointsList = new List<UserPoints> { userPoints };
        SetupMockDbSet(userPointsList);

        _contextMock
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            _handler.Handle(rejectedEvent, CancellationToken.None));

        // Assert
        exception.Should().BeNull(); // Should not rethrow
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error deducting points")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void SetupMockDbSet(List<UserPoints> userPointsList)
    {
        var queryable = userPointsList.AsQueryable();
        var mockSet = new Mock<DbSet<UserPoints>>();

        mockSet.As<IAsyncEnumerable<UserPoints>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<UserPoints>(userPointsList.GetEnumerator()));

        mockSet.As<IQueryable<UserPoints>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<UserPoints>(queryable.Provider));

        mockSet.As<IQueryable<UserPoints>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<UserPoints>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<UserPoints>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        _contextMock.Setup(c => c.UserPoints).Returns(mockSet.Object);
    }
}
