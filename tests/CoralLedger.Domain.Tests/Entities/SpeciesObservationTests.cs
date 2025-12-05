using CoralLedger.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Domain.Tests.Entities;

public class SpeciesObservationTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();

        // Act
        var speciesObs = SpeciesObservation.Create(observationId, speciesId);

        // Assert
        speciesObs.CitizenObservationId.Should().Be(observationId);
        speciesObs.BahamianSpeciesId.Should().Be(speciesId);
        speciesObs.IsAiGenerated.Should().BeFalse();
        speciesObs.RequiresExpertVerification.Should().BeFalse();
        speciesObs.IdentifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithAllOptionalParameters_SetsAllProperties()
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        var quantity = 5;
        var confidence = 95.5;
        var notes = "Large adult specimen";

        // Act
        var speciesObs = SpeciesObservation.Create(
            observationId,
            speciesId,
            quantity: quantity,
            aiConfidenceScore: confidence,
            isAiGenerated: true,
            notes: notes);

        // Assert
        speciesObs.Quantity.Should().Be(quantity);
        speciesObs.AiConfidenceScore.Should().Be(confidence);
        speciesObs.IsAiGenerated.Should().BeTrue();
        speciesObs.Notes.Should().Be(notes);
    }

    [Theory]
    [InlineData(84.9, true)]   // Below threshold
    [InlineData(85.0, false)]  // At threshold (not requiring verification)
    [InlineData(85.1, false)]  // Above threshold
    [InlineData(90.0, false)]
    [InlineData(99.9, false)]
    public void Create_WithAiConfidence_SetsRequiresExpertVerificationCorrectly(
        double confidence,
        bool expectedRequiresVerification)
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();

        // Act
        var speciesObs = SpeciesObservation.Create(
            observationId,
            speciesId,
            aiConfidenceScore: confidence);

        // Assert
        speciesObs.RequiresExpertVerification.Should().Be(expectedRequiresVerification);
    }

    [Fact]
    public void Create_WithLowAiConfidence_RequiresExpertVerification()
    {
        // Arrange - Dr. Bethel's requirement: <85% = 'Verify with expert'
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();

        // Act
        var speciesObs = SpeciesObservation.Create(
            observationId,
            speciesId,
            aiConfidenceScore: 75.0,
            isAiGenerated: true);

        // Assert
        speciesObs.RequiresExpertVerification.Should().BeTrue();
    }

    [Fact]
    public void Create_WithHighAiConfidence_DoesNotRequireExpertVerification()
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();

        // Act
        var speciesObs = SpeciesObservation.Create(
            observationId,
            speciesId,
            aiConfidenceScore: 92.0,
            isAiGenerated: true);

        // Assert
        speciesObs.RequiresExpertVerification.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNoAiConfidence_DoesNotRequireVerification()
    {
        // Arrange - Manual identification without AI
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();

        // Act
        var speciesObs = SpeciesObservation.Create(
            observationId,
            speciesId,
            isAiGenerated: false);

        // Assert
        speciesObs.RequiresExpertVerification.Should().BeFalse();
        speciesObs.AiConfidenceScore.Should().BeNull();
    }

    [Fact]
    public void MarkAsVerified_ClearsVerificationFlag()
    {
        // Arrange
        var speciesObs = SpeciesObservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            aiConfidenceScore: 70.0, // Low confidence
            isAiGenerated: true);
        speciesObs.RequiresExpertVerification.Should().BeTrue();

        // Act
        speciesObs.MarkAsVerified();

        // Assert
        speciesObs.RequiresExpertVerification.Should().BeFalse();
    }

    [Fact]
    public void UpdateConfidence_WithHighScore_ClearsVerificationFlag()
    {
        // Arrange
        var speciesObs = SpeciesObservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            aiConfidenceScore: 70.0,
            isAiGenerated: true);
        speciesObs.RequiresExpertVerification.Should().BeTrue();

        // Act
        speciesObs.UpdateConfidence(90.0);

        // Assert
        speciesObs.AiConfidenceScore.Should().Be(90.0);
        speciesObs.RequiresExpertVerification.Should().BeFalse();
    }

    [Fact]
    public void UpdateConfidence_WithLowScore_SetsVerificationFlag()
    {
        // Arrange
        var speciesObs = SpeciesObservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            aiConfidenceScore: 90.0,
            isAiGenerated: true);
        speciesObs.RequiresExpertVerification.Should().BeFalse();

        // Act
        speciesObs.UpdateConfidence(60.0);

        // Assert
        speciesObs.AiConfidenceScore.Should().Be(60.0);
        speciesObs.RequiresExpertVerification.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(84)]
    [InlineData(84.99)]
    public void UpdateConfidence_BelowThreshold_RequiresVerification(double newScore)
    {
        // Arrange
        var speciesObs = SpeciesObservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid());

        // Act
        speciesObs.UpdateConfidence(newScore);

        // Assert
        speciesObs.RequiresExpertVerification.Should().BeTrue();
    }

    [Theory]
    [InlineData(85)]
    [InlineData(85.01)]
    [InlineData(90)]
    [InlineData(100)]
    public void UpdateConfidence_AtOrAboveThreshold_DoesNotRequireVerification(double newScore)
    {
        // Arrange
        var speciesObs = SpeciesObservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            aiConfidenceScore: 50.0); // Start with low confidence

        // Act
        speciesObs.UpdateConfidence(newScore);

        // Assert
        speciesObs.RequiresExpertVerification.Should().BeFalse();
    }

    [Fact]
    public void Create_WithQuantity_SetsQuantity()
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();

        // Act
        var speciesObs = SpeciesObservation.Create(
            observationId,
            speciesId,
            quantity: 12);

        // Assert
        speciesObs.Quantity.Should().Be(12);
    }

    [Fact]
    public void Create_GeneratesNewId()
    {
        // Arrange & Act
        var speciesObs1 = SpeciesObservation.Create(Guid.NewGuid(), Guid.NewGuid());
        var speciesObs2 = SpeciesObservation.Create(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        speciesObs1.Id.Should().NotBeEmpty();
        speciesObs2.Id.Should().NotBeEmpty();
        speciesObs1.Id.Should().NotBe(speciesObs2.Id);
    }
}
