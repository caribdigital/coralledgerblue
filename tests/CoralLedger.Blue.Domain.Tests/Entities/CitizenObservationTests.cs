using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class CitizenObservationTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private static Point CreateTestPoint(double lat = 24.5, double lon = -77.5)
    {
        return GeometryFactory.CreatePoint(new Coordinate(lon, lat));
    }

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var location = CreateTestPoint();
        var observationTime = DateTime.UtcNow.AddHours(-1);
        var title = "Coral bleaching observed";
        var type = ObservationType.CoralBleaching;

        // Act
        var observation = CitizenObservation.Create(
            location,
            observationTime,
            title,
            type);

        // Assert
        observation.Location.Should().Be(location);
        observation.ObservationTime.Should().Be(observationTime);
        observation.Title.Should().Be(title);
        observation.Type.Should().Be(type);
        observation.Status.Should().Be(ObservationStatus.Pending);
        observation.Severity.Should().Be(3); // Default
        observation.Id.Should().NotBeEmpty();
        observation.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithAllOptionalParameters_SetsAllProperties()
    {
        // Arrange
        var location = CreateTestPoint();
        var observationTime = DateTime.UtcNow;
        var title = "Lionfish sighting";
        var type = ObservationType.FishSighting;
        var description = "Large lionfish near reef edge";
        var severity = 5;
        var email = "citizen@example.com";
        var name = "John Diver";

        // Act
        var observation = CitizenObservation.Create(
            location,
            observationTime,
            title,
            type,
            description: description,
            severity: severity,
            citizenEmail: email,
            citizenName: name);

        // Assert
        observation.Description.Should().Be(description);
        observation.Severity.Should().Be(severity);
        observation.CitizenEmail.Should().Be(email);
        observation.CitizenName.Should().Be(name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void Create_WithInvalidSeverity_ThrowsArgumentOutOfRangeException(int severity)
    {
        // Arrange & Act
        var act = () => CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test",
            ObservationType.Other,
            severity: severity);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(severity))
            .WithMessage("*Severity must be between 1 and 5*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Create_WithValidSeverity_SetsSeverity(int severity)
    {
        // Act
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test",
            ObservationType.Other,
            severity: severity);

        // Assert
        observation.Severity.Should().Be(severity);
    }

    [Fact]
    public void SetMpaContext_UpdatesContextAndModifiedAt()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);

        var mpaId = Guid.NewGuid();
        var reefId = Guid.NewGuid();

        // Act
        observation.SetMpaContext(true, mpaId, reefId);

        // Assert
        observation.IsInMpa.Should().BeTrue();
        observation.MarineProtectedAreaId.Should().Be(mpaId);
        observation.ReefId.Should().Be(reefId);
        observation.ModifiedAt.Should().NotBeNull();
        observation.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SetMpaContext_WhenOutsideMpa_SetsIsInMpaFalse()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);

        // Act
        observation.SetMpaContext(false);

        // Assert
        observation.IsInMpa.Should().BeFalse();
        observation.MarineProtectedAreaId.Should().BeNull();
        observation.ReefId.Should().BeNull();
    }

    [Fact]
    public void Approve_SetsStatusAndModerationFields()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);
        var notes = "Verified by marine biologist";

        // Act
        observation.Approve(notes);

        // Assert
        observation.Status.Should().Be(ObservationStatus.Approved);
        observation.ModerationNotes.Should().Be(notes);
        observation.ModeratedAt.Should().NotBeNull();
        observation.ModeratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        observation.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WithoutNotes_SetsStatusOnly()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);

        // Act
        observation.Approve();

        // Assert
        observation.Status.Should().Be(ObservationStatus.Approved);
        observation.ModerationNotes.Should().BeNull();
        observation.ModeratedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reject_SetsStatusAndReason()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);
        var reason = "Location appears to be on land";

        // Act
        observation.Reject(reason);

        // Assert
        observation.Status.Should().Be(ObservationStatus.Rejected);
        observation.ModerationNotes.Should().Be(reason);
        observation.ModeratedAt.Should().NotBeNull();
        observation.ModeratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        observation.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void RequestReview_SetsStatusAndReason()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);
        var reason = "Species identification uncertain";

        // Act
        observation.RequestReview(reason);

        // Assert
        observation.Status.Should().Be(ObservationStatus.NeedsReview);
        observation.ModerationNotes.Should().Be(reason);
        observation.ModeratedAt.Should().BeNull(); // Not moderated yet
        observation.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddPhoto_AddsPhotoToCollection()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);

        var photo = ObservationPhoto.Create(
            observation.Id,
            "photos/test.jpg",
            "https://storage.example.com/photos/test.jpg",
            "image/jpeg",
            1024);

        // Act
        observation.AddPhoto(photo);

        // Assert
        observation.Photos.Should().Contain(photo);
        observation.Photos.Should().HaveCount(1);
        observation.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddPhoto_MultipleTimes_AddsAllPhotos()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.Other);

        var photo1 = ObservationPhoto.Create(observation.Id, "photo1.jpg", "uri1", "image/jpeg", 1000);
        var photo2 = ObservationPhoto.Create(observation.Id, "photo2.jpg", "uri2", "image/jpeg", 2000);
        var photo3 = ObservationPhoto.Create(observation.Id, "photo3.jpg", "uri3", "image/jpeg", 3000);

        // Act
        observation.AddPhoto(photo1);
        observation.AddPhoto(photo2);
        observation.AddPhoto(photo3);

        // Assert
        observation.Photos.Should().HaveCount(3);
        observation.Photos.Should().Contain(photo1);
        observation.Photos.Should().Contain(photo2);
        observation.Photos.Should().Contain(photo3);
    }

    [Theory]
    [InlineData(ObservationType.CoralBleaching)]
    [InlineData(ObservationType.ReefHealth)]
    [InlineData(ObservationType.FishSighting)]
    [InlineData(ObservationType.MarineDebris)]
    [InlineData(ObservationType.IllegalFishing)]
    [InlineData(ObservationType.BoatAnchorDamage)]
    [InlineData(ObservationType.WildlifeSighting)]
    [InlineData(ObservationType.WaterQuality)]
    [InlineData(ObservationType.Other)]
    public void Create_SupportsAllObservationTypes(ObservationType type)
    {
        // Act
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            type);

        // Assert
        observation.Type.Should().Be(type);
    }

    [Fact]
    public void StatusTransitions_FromPending_CanTransitionToAllStates()
    {
        // Arrange - Start with pending observation
        var observation1 = CitizenObservation.Create(CreateTestPoint(), DateTime.UtcNow, "Test", ObservationType.Other);
        var observation2 = CitizenObservation.Create(CreateTestPoint(), DateTime.UtcNow, "Test", ObservationType.Other);
        var observation3 = CitizenObservation.Create(CreateTestPoint(), DateTime.UtcNow, "Test", ObservationType.Other);

        // Act & Assert - Can transition to all states
        observation1.Approve();
        observation1.Status.Should().Be(ObservationStatus.Approved);

        observation2.Reject("Invalid");
        observation2.Status.Should().Be(ObservationStatus.Rejected);

        observation3.RequestReview("Uncertain");
        observation3.Status.Should().Be(ObservationStatus.NeedsReview);
    }

    [Fact]
    public void AwardPoints_ForApprovedObservation_SetsPointsAndMarksProcessed()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.CoralBleaching);
        observation.Approve();

        // Act
        observation.AwardPoints(50);

        // Assert
        observation.PointsAwarded.Should().Be(50);
        observation.PointsProcessed.Should().BeTrue();
        observation.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void AwardPoints_ForPendingObservation_ThrowsInvalidOperationException()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.CoralBleaching);

        // Act
        var act = () => observation.AwardPoints(50);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Can only award points for approved observations*");
    }

    [Fact]
    public void AwardPoints_WhenAlreadyProcessed_ThrowsInvalidOperationException()
    {
        // Arrange
        var observation = CitizenObservation.Create(
            CreateTestPoint(),
            DateTime.UtcNow,
            "Test observation",
            ObservationType.CoralBleaching);
        observation.Approve();
        observation.AwardPoints(50);

        // Act
        var act = () => observation.AwardPoints(25);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Points already awarded for this observation*");
    }
}
