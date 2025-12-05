using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Domain.Tests.Entities;

public class BahamianSpeciesTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var scientificName = "Pterois volitans";
        var commonName = "Lionfish";
        var category = SpeciesCategory.Fish;
        var status = ConservationStatus.LeastConcern;

        // Act
        var species = BahamianSpecies.Create(
            scientificName,
            commonName,
            category,
            status);

        // Assert
        species.ScientificName.Should().Be(scientificName);
        species.CommonName.Should().Be(commonName);
        species.Category.Should().Be(category);
        species.ConservationStatus.Should().Be(status);
        species.IsInvasive.Should().BeFalse();
    }

    [Fact]
    public void Create_WithAllOptionalParameters_SetsAllProperties()
    {
        // Arrange
        var scientificName = "Pterois volitans";
        var commonName = "Lionfish";
        var localName = "Devil Fish";
        var category = SpeciesCategory.Fish;
        var status = ConservationStatus.LeastConcern;
        var description = "Invasive species from Indo-Pacific";
        var identificationTips = "Look for distinctive stripes";
        var habitat = "Coral reefs";
        var minDepth = 1;
        var maxDepth = 55;

        // Act
        var species = BahamianSpecies.Create(
            scientificName,
            commonName,
            category,
            status,
            localName: localName,
            isInvasive: true,
            description: description,
            identificationTips: identificationTips,
            habitat: habitat,
            typicalDepthMinM: minDepth,
            typicalDepthMaxM: maxDepth);

        // Assert
        species.LocalName.Should().Be(localName);
        species.IsInvasive.Should().BeTrue();
        species.Description.Should().Be(description);
        species.IdentificationTips.Should().Be(identificationTips);
        species.Habitat.Should().Be(habitat);
        species.TypicalDepthMinM.Should().Be(minDepth);
        species.TypicalDepthMaxM.Should().Be(maxDepth);
    }

    [Fact]
    public void Create_WithEmptyScientificName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => BahamianSpecies.Create(
            "",
            "Lionfish",
            SpeciesCategory.Fish,
            ConservationStatus.LeastConcern);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("scientificName")
            .WithMessage("*Scientific name is required*");
    }

    [Fact]
    public void Create_WithWhitespaceScientificName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => BahamianSpecies.Create(
            "   ",
            "Lionfish",
            SpeciesCategory.Fish,
            ConservationStatus.LeastConcern);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("scientificName");
    }

    [Fact]
    public void Create_WithEmptyCommonName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => BahamianSpecies.Create(
            "Pterois volitans",
            "",
            SpeciesCategory.Fish,
            ConservationStatus.LeastConcern);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("commonName")
            .WithMessage("*Common name is required*");
    }

    [Theory]
    [InlineData(ConservationStatus.LeastConcern, false)]
    [InlineData(ConservationStatus.NearThreatened, false)]
    [InlineData(ConservationStatus.Vulnerable, true)]
    [InlineData(ConservationStatus.Endangered, true)]
    [InlineData(ConservationStatus.CriticallyEndangered, true)]
    public void IsThreatened_ReturnsCorrectValue(ConservationStatus status, bool expectedThreatened)
    {
        // Arrange
        var species = BahamianSpecies.Create(
            "Test species",
            "Test",
            SpeciesCategory.Fish,
            status);

        // Assert
        species.IsThreatened.Should().Be(expectedThreatened);
    }

    [Theory]
    [InlineData(ConservationStatus.LeastConcern, false, false)]
    [InlineData(ConservationStatus.NearThreatened, false, false)]
    [InlineData(ConservationStatus.Vulnerable, false, false)]
    [InlineData(ConservationStatus.Endangered, false, true)]
    [InlineData(ConservationStatus.CriticallyEndangered, false, true)]
    [InlineData(ConservationStatus.LeastConcern, true, true)] // Invasive
    public void RequiresPriorityAlert_ReturnsCorrectValue(
        ConservationStatus status,
        bool isInvasive,
        bool expectedPriority)
    {
        // Arrange
        var species = BahamianSpecies.Create(
            "Test species",
            "Test",
            SpeciesCategory.Fish,
            status,
            isInvasive: isInvasive);

        // Assert
        species.RequiresPriorityAlert.Should().Be(expectedPriority);
    }

    [Fact]
    public void Create_Lionfish_ShouldBeFlaggedForPriorityAlert()
    {
        // Arrange - Lionfish is invasive
        var lionfish = BahamianSpecies.Create(
            "Pterois volitans",
            "Lionfish",
            SpeciesCategory.Fish,
            ConservationStatus.LeastConcern,
            isInvasive: true);

        // Assert
        lionfish.IsInvasive.Should().BeTrue();
        lionfish.RequiresPriorityAlert.Should().BeTrue();
    }

    [Fact]
    public void Create_NassauGrouper_ShouldBeFlaggedForPriorityAlert()
    {
        // Arrange - Nassau Grouper is critically endangered
        var nassauGrouper = BahamianSpecies.Create(
            "Epinephelus striatus",
            "Nassau Grouper",
            SpeciesCategory.Fish,
            ConservationStatus.CriticallyEndangered);

        // Assert
        nassauGrouper.IsThreatened.Should().BeTrue();
        nassauGrouper.RequiresPriorityAlert.Should().BeTrue();
    }

    [Fact]
    public void Create_QueenConch_ShouldBeThreatened()
    {
        // Arrange - Queen Conch is vulnerable
        var queenConch = BahamianSpecies.Create(
            "Aliger gigas",
            "Queen Conch",
            SpeciesCategory.Invertebrate,
            ConservationStatus.Vulnerable,
            localName: "Conch");

        // Assert
        queenConch.IsThreatened.Should().BeTrue();
        queenConch.LocalName.Should().Be("Conch");
    }

    [Theory]
    [InlineData(SpeciesCategory.Fish)]
    [InlineData(SpeciesCategory.Coral)]
    [InlineData(SpeciesCategory.Invertebrate)]
    [InlineData(SpeciesCategory.Mammal)]
    [InlineData(SpeciesCategory.Reptile)]
    [InlineData(SpeciesCategory.Seabird)]
    [InlineData(SpeciesCategory.Seagrass)]
    [InlineData(SpeciesCategory.Algae)]
    [InlineData(SpeciesCategory.Sponge)]
    [InlineData(SpeciesCategory.Other)]
    public void Create_SupportsAllCategories(SpeciesCategory category)
    {
        // Arrange & Act
        var species = BahamianSpecies.Create(
            "Test species",
            "Test",
            category,
            ConservationStatus.LeastConcern);

        // Assert
        species.Category.Should().Be(category);
    }

    [Fact]
    public void Create_WithDepthRange_SetsDepthCorrectly()
    {
        // Arrange & Act
        var species = BahamianSpecies.Create(
            "Acropora palmata",
            "Elkhorn Coral",
            SpeciesCategory.Coral,
            ConservationStatus.CriticallyEndangered,
            typicalDepthMinM: 0,
            typicalDepthMaxM: 20);

        // Assert
        species.TypicalDepthMinM.Should().Be(0);
        species.TypicalDepthMaxM.Should().Be(20);
    }
}
