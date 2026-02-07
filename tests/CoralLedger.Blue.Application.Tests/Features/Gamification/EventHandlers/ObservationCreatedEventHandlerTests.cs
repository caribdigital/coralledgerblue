using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardPoints;
using CoralLedger.Blue.Application.Features.Gamification.EventHandlers;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoralLedger.Blue.Application.Tests.Features.Gamification.EventHandlers;

public class ObservationCreatedEventHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<ObservationCreatedEventHandler>> _loggerMock;
    private readonly ObservationCreatedEventHandler _handler;

    public ObservationCreatedEventHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<ObservationCreatedEventHandler>>();
        _handler = new ObservationCreatedEventHandler(_mediatorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithBasicObservation_Awards5Points()
    {
        // Arrange
        var observationEvent = new ObservationCreatedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: "test@example.com",
            Type: ObservationType.ReefHealth,
            HasPhotos: false,
            HasLocation: true,
            IsInMpa: false
        );

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AwardPointsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardPointsResult(Success: true, TotalPoints: 7));

        // Act
        await _handler.Handle(observationEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AwardPointsCommand>(cmd =>
                    cmd.CitizenEmail == "test@example.com" &&
                    cmd.Points == 7), // 5 base + 2 GPS
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPhotosAndMpa_AwardsAllBonuses()
    {
        // Arrange
        var observationEvent = new ObservationCreatedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: "test@example.com",
            Type: ObservationType.CoralBleaching,
            HasPhotos: true,
            HasLocation: true,
            IsInMpa: true
        );

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AwardPointsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardPointsResult(Success: true, TotalPoints: 15));

        // Act
        await _handler.Handle(observationEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AwardPointsCommand>(cmd =>
                    cmd.CitizenEmail == "test@example.com" &&
                    cmd.Points == 15), // 5 base + 3 photo + 2 GPS + 5 MPA
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoEmail_SkipsAwardingPoints()
    {
        // Arrange
        var observationEvent = new ObservationCreatedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: null,
            Type: ObservationType.ReefHealth,
            HasPhotos: false,
            HasLocation: true,
            IsInMpa: false
        );

        // Act
        await _handler.Handle(observationEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AwardPointsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_SkipsAwardingPoints()
    {
        // Arrange
        var observationEvent = new ObservationCreatedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: "   ",
            Type: ObservationType.ReefHealth,
            HasPhotos: false,
            HasLocation: true,
            IsInMpa: false
        );

        // Act
        await _handler.Handle(observationEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AwardPointsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAwardPointsFails_LogsWarning()
    {
        // Arrange
        var observationEvent = new ObservationCreatedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: "test@example.com",
            Type: ObservationType.ReefHealth,
            HasPhotos: false,
            HasLocation: true,
            IsInMpa: false
        );

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AwardPointsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardPointsResult(Success: false, Error: "Database error"));

        // Act
        await _handler.Handle(observationEvent, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to award points")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
