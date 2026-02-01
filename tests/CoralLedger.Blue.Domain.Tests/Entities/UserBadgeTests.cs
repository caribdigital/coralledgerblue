using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class UserBadgeTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var email = "test@example.com";
        var badgeType = BadgeType.FirstObservation;
        var description = "Completed your first observation!";

        // Act
        var badge = UserBadge.Create(email, badgeType, description);

        // Assert
        badge.CitizenEmail.Should().Be(email);
        badge.BadgeType.Should().Be(badgeType);
        badge.Description.Should().Be(description);
        badge.Id.Should().NotBeEmpty();
        badge.EarnedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        badge.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithoutDescription_CreatesSuccessfully()
    {
        // Act
        var badge = UserBadge.Create("test@example.com", BadgeType.SpeciesExpert);

        // Assert
        badge.Description.Should().BeNull();
        badge.BadgeType.Should().Be(BadgeType.SpeciesExpert);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        // Act
        var act = () => UserBadge.Create(email!, BadgeType.FirstObservation);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("citizenEmail")
            .WithMessage("*Citizen email is required*");
    }

    [Theory]
    [InlineData(BadgeType.FirstObservation)]
    [InlineData(BadgeType.TenObservations)]
    [InlineData(BadgeType.SpeciesExpert)]
    [InlineData(BadgeType.CoralExpert)]
    [InlineData(BadgeType.PhotoPro)]
    [InlineData(BadgeType.MPAGuardian)]
    [InlineData(BadgeType.WeeklyContributor)]
    public void Create_SupportsAllBadgeTypes(BadgeType badgeType)
    {
        // Act
        var badge = UserBadge.Create("test@example.com", badgeType);

        // Assert
        badge.BadgeType.Should().Be(badgeType);
    }
}
