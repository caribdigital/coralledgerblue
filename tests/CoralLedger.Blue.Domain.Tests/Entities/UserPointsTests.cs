using CoralLedger.Blue.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class UserPointsTests
{
    [Fact]
    public void Create_WithValidEmail_InitializesWithZeroPoints()
    {
        // Arrange
        var email = "test@example.com";

        // Act
        var userPoints = UserPoints.Create(email);

        // Assert
        userPoints.CitizenEmail.Should().Be(email);
        userPoints.TotalPoints.Should().Be(0);
        userPoints.WeeklyPoints.Should().Be(0);
        userPoints.MonthlyPoints.Should().Be(0);
        userPoints.Id.Should().NotBeEmpty();
        userPoints.LastPointsEarned.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        userPoints.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string? email)
    {
        // Act
        var act = () => UserPoints.Create(email!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("citizenEmail")
            .WithMessage("*Citizen email is required*");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void AddPoints_WithValidPoints_IncreasesAllCategories(int points)
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");

        // Act
        userPoints.AddPoints(points);

        // Assert
        userPoints.TotalPoints.Should().Be(points);
        userPoints.WeeklyPoints.Should().Be(points);
        userPoints.MonthlyPoints.Should().Be(points);
        userPoints.LastPointsEarned.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        userPoints.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddPoints_MultipleTimes_AccumulatesPoints()
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");

        // Act
        userPoints.AddPoints(10);
        userPoints.AddPoints(20);
        userPoints.AddPoints(30);

        // Assert
        userPoints.TotalPoints.Should().Be(60);
        userPoints.WeeklyPoints.Should().Be(60);
        userPoints.MonthlyPoints.Should().Be(60);
    }

    [Fact]
    public void AddPoints_WithNegativePoints_ThrowsArgumentException()
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");

        // Act
        var act = () => userPoints.AddPoints(-10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("points")
            .WithMessage("*Points must be positive*");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void DeductPoints_WithValidPoints_DecreasesAllCategories(int points)
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");
        userPoints.AddPoints(100);

        // Act
        userPoints.DeductPoints(points, "Test reason");

        // Assert
        userPoints.TotalPoints.Should().Be(100 - points);
        userPoints.WeeklyPoints.Should().Be(100 - points);
        userPoints.MonthlyPoints.Should().Be(100 - points);
        userPoints.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void DeductPoints_MoreThanAvailable_DoesNotGoBelowZero()
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");
        userPoints.AddPoints(50);

        // Act
        userPoints.DeductPoints(100, "Test reason");

        // Assert
        userPoints.TotalPoints.Should().Be(0);
        userPoints.WeeklyPoints.Should().Be(0);
        userPoints.MonthlyPoints.Should().Be(0);
    }

    [Fact]
    public void DeductPoints_WithNegativePoints_ThrowsArgumentException()
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");
        userPoints.AddPoints(100);

        // Act
        var act = () => userPoints.DeductPoints(-10, "Test reason");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("points")
            .WithMessage("*Points must be positive*");
    }

    [Fact]
    public void AddPoints_SetsResetDates()
    {
        // Arrange
        var userPoints = UserPoints.Create("test@example.com");

        // Act
        userPoints.AddPoints(10);

        // Assert
        userPoints.WeeklyResetAt.Should().NotBeNull();
        userPoints.MonthlyResetAt.Should().NotBeNull();
        userPoints.WeeklyResetAt.Should().BeAfter(DateTime.UtcNow);
        userPoints.MonthlyResetAt.Should().BeAfter(DateTime.UtcNow);
    }
}
