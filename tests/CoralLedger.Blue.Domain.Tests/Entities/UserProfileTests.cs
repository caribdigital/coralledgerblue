using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class UserProfileTests
{
    [Fact]
    public void Create_WithValidEmail_InitializesWithDefaults()
    {
        // Arrange
        var email = "test@example.com";
        var name = "Test User";

        // Act
        var profile = UserProfile.Create(email, name);

        // Assert
        profile.CitizenEmail.Should().Be(email);
        profile.CitizenName.Should().Be(name);
        profile.Tier.Should().Be(ObserverTier.None);
        profile.TotalObservations.Should().Be(0);
        profile.VerifiedObservations.Should().Be(0);
        profile.RejectedObservations.Should().Be(0);
        profile.AccuracyRate.Should().Be(0);
        profile.Id.Should().NotBeEmpty();
        profile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithoutName_CreatesSuccessfully()
    {
        // Act
        var profile = UserProfile.Create("test@example.com");

        // Assert
        profile.CitizenEmail.Should().Be("test@example.com");
        profile.CitizenName.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        // Act
        var act = () => UserProfile.Create(email!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("citizenEmail")
            .WithMessage("*Citizen email is required*");
    }

    [Fact]
    public void IncrementObservations_IncreasesCount()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");

        // Act
        profile.IncrementObservations();

        // Assert
        profile.TotalObservations.Should().Be(1);
        profile.LastObservationAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        profile.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordVerifiedObservation_UpdatesStatsAndAccuracy()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");

        // Act
        profile.RecordVerifiedObservation();

        // Assert
        profile.VerifiedObservations.Should().Be(1);
        profile.AccuracyRate.Should().Be(100);
        profile.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordRejectedObservation_UpdatesStatsAndAccuracy()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");
        profile.RecordVerifiedObservation();

        // Act
        profile.RecordRejectedObservation();

        // Assert
        profile.RejectedObservations.Should().Be(1);
        profile.AccuracyRate.Should().Be(50); // 1 verified out of 2 total
        profile.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateName_ChangesName()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com", "Old Name");

        // Act
        profile.UpdateName("New Name");

        // Assert
        profile.CitizenName.Should().Be("New Name");
        profile.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Tier_WithBronzeRequirements_PromotesToBronze()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");

        // Act - 10 verified observations, 3 rejected (76.9% accuracy)
        for (int i = 0; i < 10; i++)
        {
            profile.RecordVerifiedObservation();
        }
        for (int i = 0; i < 3; i++)
        {
            profile.RecordRejectedObservation();
        }

        // Assert
        profile.Tier.Should().Be(ObserverTier.Bronze);
        profile.VerifiedObservations.Should().Be(10);
        profile.AccuracyRate.Should().BeGreaterOrEqualTo(70);
    }

    [Fact]
    public void Tier_WithSilverRequirements_PromotesToSilver()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");

        // Act - 50 verified observations, 10 rejected (83.3% accuracy)
        for (int i = 0; i < 50; i++)
        {
            profile.RecordVerifiedObservation();
        }
        for (int i = 0; i < 10; i++)
        {
            profile.RecordRejectedObservation();
        }

        // Assert
        profile.Tier.Should().Be(ObserverTier.Silver);
        profile.VerifiedObservations.Should().Be(50);
        profile.AccuracyRate.Should().BeGreaterOrEqualTo(80);
    }

    [Fact]
    public void Tier_WithGoldRequirements_PromotesToGold()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");

        // Act - 100 verified observations, 5 rejected (95.2% accuracy)
        for (int i = 0; i < 100; i++)
        {
            profile.RecordVerifiedObservation();
        }
        for (int i = 0; i < 5; i++)
        {
            profile.RecordRejectedObservation();
        }

        // Assert
        profile.Tier.Should().Be(ObserverTier.Gold);
        profile.VerifiedObservations.Should().Be(100);
        profile.AccuracyRate.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public void Tier_WithInsufficientAccuracy_RemainsNone()
    {
        // Arrange
        var profile = UserProfile.Create("test@example.com");

        // Act - 10 verified, 10 rejected (50% accuracy - below 70% requirement)
        for (int i = 0; i < 10; i++)
        {
            profile.RecordVerifiedObservation();
        }
        for (int i = 0; i < 10; i++)
        {
            profile.RecordRejectedObservation();
        }

        // Assert
        profile.Tier.Should().Be(ObserverTier.None);
        profile.AccuracyRate.Should().Be(50);
    }
}
