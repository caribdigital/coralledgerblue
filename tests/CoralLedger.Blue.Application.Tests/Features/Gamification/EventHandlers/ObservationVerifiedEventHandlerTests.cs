using CoralLedger.Blue.Application.Common.Events;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardBadge;
using CoralLedger.Blue.Application.Features.Gamification.EventHandlers;
using CoralLedger.Blue.Application.Tests.TestFixtures;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Application.Tests.Features.Gamification.EventHandlers;

public class ObservationVerifiedEventHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IMarineDbContext> _contextMock;
    private readonly Mock<ILogger<ObservationVerifiedEventHandler>> _loggerMock;
    private readonly ObservationVerifiedEventHandler _handler;
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    public ObservationVerifiedEventHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _contextMock = new Mock<IMarineDbContext>();
        _loggerMock = new Mock<ILogger<ObservationVerifiedEventHandler>>();
        _handler = new ObservationVerifiedEventHandler(_mediatorMock.Object, _contextMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithFirstVerifiedObservation_AwardsFirstVerifiedBadge()
    {
        // Arrange
        var citizenEmail = "test@example.com";
        var verifiedEvent = new ObservationVerifiedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            PointsAwarded: 15
        );

        var observations = new List<CitizenObservation>
        {
            CreateTestObservation(citizenEmail, ObservationStatus.Approved)
        };

        SetupMockDbSet(observations);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AwardBadgeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardBadgeResult(Success: true, BadgeId: Guid.NewGuid()));

        // Act
        await _handler.Handle(verifiedEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AwardBadgeCommand>(cmd =>
                    cmd.CitizenEmail == citizenEmail &&
                    cmd.BadgeType == BadgeType.FirstVerifiedObservation),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithTenObservations_AwardsTenObservationsBadge()
    {
        // Arrange
        var citizenEmail = "test@example.com";
        var verifiedEvent = new ObservationVerifiedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            PointsAwarded: 15
        );

        var observations = CreateMultipleTestObservations(citizenEmail, 10, ObservationStatus.Approved);
        SetupMockDbSet(observations);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AwardBadgeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardBadgeResult(Success: true, BadgeId: Guid.NewGuid()));

        // Act
        await _handler.Handle(verifiedEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AwardBadgeCommand>(cmd =>
                    cmd.CitizenEmail == citizenEmail &&
                    cmd.BadgeType == BadgeType.TenObservations),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WithBleachingObservation_AwardsBleachingDetectorBadge()
    {
        // Arrange
        var citizenEmail = "test@example.com";
        var verifiedEvent = new ObservationVerifiedEvent(
            ObservationId: Guid.NewGuid(),
            CitizenEmail: citizenEmail,
            PointsAwarded: 15
        );

        var observations = new List<CitizenObservation>
        {
            CreateTestObservation(citizenEmail, ObservationStatus.Approved, ObservationType.CoralBleaching)
        };

        SetupMockDbSet(observations);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AwardBadgeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardBadgeResult(Success: true, BadgeId: Guid.NewGuid()));

        // Act
        await _handler.Handle(verifiedEvent, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AwardBadgeCommand>(cmd =>
                    cmd.CitizenEmail == citizenEmail &&
                    cmd.BadgeType == BadgeType.BleachingDetector),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CitizenObservation CreateTestObservation(
        string email,
        ObservationStatus status,
        ObservationType type = ObservationType.ReefHealth)
    {
        var location = GeometryFactory.CreatePoint(new Coordinate(-77.5, 24.5));
        var observation = CitizenObservation.Create(
            location,
            DateTime.UtcNow,
            "Test Observation",
            type,
            "Test description",
            3,
            email,
            "Test User");

        if (status == ObservationStatus.Approved)
        {
            observation.Approve();
        }
        else if (status == ObservationStatus.Rejected)
        {
            observation.Reject("Test rejection");
        }

        return observation;
    }

    private static List<CitizenObservation> CreateMultipleTestObservations(
        string email,
        int count,
        ObservationStatus status)
    {
        var observations = new List<CitizenObservation>();
        for (int i = 0; i < count; i++)
        {
            observations.Add(CreateTestObservation(email, status));
        }
        return observations;
    }

    private void SetupMockDbSet(List<CitizenObservation> observations)
    {
        var queryable = observations.AsQueryable();
        var mockSet = new Mock<DbSet<CitizenObservation>>();

        mockSet.As<IAsyncEnumerable<CitizenObservation>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<CitizenObservation>(observations.GetEnumerator()));

        mockSet.As<IQueryable<CitizenObservation>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<CitizenObservation>(queryable.Provider));

        mockSet.As<IQueryable<CitizenObservation>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<CitizenObservation>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<CitizenObservation>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        _contextMock.Setup(c => c.CitizenObservations).Returns(mockSet.Object);
    }
}
