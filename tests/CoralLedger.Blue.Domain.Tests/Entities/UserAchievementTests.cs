using CoralLedger.Blue.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class UserAchievementTests
{
    [Fact]
    public void Create_WithValidData_InitializesCorrectly()
    {
        // Arrange
        var email = "test@example.com";
        var key = "first_10_observations";
        var title = "Getting Started";
        var target = 10;
        var description = "Complete 10 observations";
        var points = 50;

        // Act
        var achievement = UserAchievement.Create(email, key, title, target, description, points);

        // Assert
        achievement.CitizenEmail.Should().Be(email);
        achievement.AchievementKey.Should().Be(key);
        achievement.Title.Should().Be(title);
        achievement.Description.Should().Be(description);
        achievement.TargetProgress.Should().Be(target);
        achievement.PointsAwarded.Should().Be(points);
        achievement.CurrentProgress.Should().Be(0);
        achievement.IsCompleted.Should().BeFalse();
        achievement.CompletedAt.Should().BeNull();
        achievement.Id.Should().NotBeEmpty();
        achievement.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        // Act
        var act = () => UserAchievement.Create(email!, "key", "Title", 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("citizenEmail")
            .WithMessage("*Citizen email is required*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidKey_ThrowsArgumentException(string? key)
    {
        // Act
        var act = () => UserAchievement.Create("test@example.com", key!, "Title", 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("achievementKey")
            .WithMessage("*Achievement key is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidTarget_ThrowsArgumentException(int target)
    {
        // Act
        var act = () => UserAchievement.Create("test@example.com", "key", "Title", target);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetProgress")
            .WithMessage("*Target progress must be positive*");
    }

    [Fact]
    public void UpdateProgress_WithPartialProgress_UpdatesCurrentProgress()
    {
        // Arrange
        var achievement = UserAchievement.Create("test@example.com", "key", "Title", 10);

        // Act
        var completed = achievement.UpdateProgress(5);

        // Assert
        completed.Should().BeFalse();
        achievement.CurrentProgress.Should().Be(5);
        achievement.IsCompleted.Should().BeFalse();
        achievement.CompletedAt.Should().BeNull();
        achievement.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProgress_ReachingTarget_CompletesAchievement()
    {
        // Arrange
        var achievement = UserAchievement.Create("test@example.com", "key", "Title", 10);

        // Act
        var completed = achievement.UpdateProgress(10);

        // Assert
        completed.Should().BeTrue();
        achievement.CurrentProgress.Should().Be(10);
        achievement.IsCompleted.Should().BeTrue();
        achievement.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        achievement.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProgress_ExceedingTarget_CapsAtTarget()
    {
        // Arrange
        var achievement = UserAchievement.Create("test@example.com", "key", "Title", 10);

        // Act
        var completed = achievement.UpdateProgress(15);

        // Assert
        completed.Should().BeTrue();
        achievement.CurrentProgress.Should().Be(10); // Capped at target
        achievement.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void UpdateProgress_WhenAlreadyCompleted_ReturnsFalse()
    {
        // Arrange
        var achievement = UserAchievement.Create("test@example.com", "key", "Title", 10);
        achievement.UpdateProgress(10); // Complete it

        // Act
        var completed = achievement.UpdateProgress(11);

        // Assert
        completed.Should().BeFalse();
        achievement.CurrentProgress.Should().Be(10);
        achievement.IsCompleted.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(5, 10, 50)]
    [InlineData(10, 10, 100)]
    [InlineData(7, 10, 70)]
    public void GetProgressPercentage_ReturnsCorrectPercentage(int current, int target, int expectedPercent)
    {
        // Arrange
        var achievement = UserAchievement.Create("test@example.com", "key", "Title", target);
        achievement.UpdateProgress(current);

        // Act
        var percentage = achievement.GetProgressPercentage();

        // Assert
        percentage.Should().Be(expectedPercent);
    }
}
