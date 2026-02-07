using CoralLedger.Blue.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class EmailVerificationTokenTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expirationHours = 48;

        // Act
        var token = EmailVerificationToken.Create(userId, expirationHours);

        // Assert
        token.Id.Should().NotBe(Guid.Empty);
        token.UserId.Should().Be(userId);
        token.Token.Should().NotBeNullOrEmpty();
        token.Token.Length.Should().BeGreaterThan(20, "token should be sufficiently long");
        token.IsUsed.Should().BeFalse();
        token.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(expirationHours), TimeSpan.FromSeconds(5));
        token.UsedAt.Should().BeNull();
    }

    [Fact]
    public void Create_GeneratesUniqueTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token1 = EmailVerificationToken.Create(userId);
        var token2 = EmailVerificationToken.Create(userId);

        // Assert
        token1.Token.Should().NotBe(token2.Token);
        token1.Id.Should().NotBe(token2.Id);
    }

    [Fact]
    public void Create_WithDefaultExpiration_ExpiresIn48Hours()
    {
        // Arrange & Act
        var token = EmailVerificationToken.Create(Guid.NewGuid());

        // Assert
        var expectedExpiration = DateTime.UtcNow.AddHours(48);
        token.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsValid_ForNewToken_ReturnsTrue()
    {
        // Arrange
        var token = EmailVerificationToken.Create(Guid.NewGuid());

        // Act
        var isValid = token.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ForUsedToken_ReturnsFalse()
    {
        // Arrange
        var token = EmailVerificationToken.Create(Guid.NewGuid());
        token.MarkAsUsed();

        // Act
        var isValid = token.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MarkAsUsed_SetsIsUsedToTrue()
    {
        // Arrange
        var token = EmailVerificationToken.Create(Guid.NewGuid());

        // Act
        token.MarkAsUsed();

        // Assert
        token.IsUsed.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
        token.UsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAsUsed_WhenAlreadyUsed_ThrowsException()
    {
        // Arrange
        var token = EmailVerificationToken.Create(Guid.NewGuid());
        token.MarkAsUsed();

        // Act
        var action = () => token.MarkAsUsed();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Token has already been used");
    }

    [Fact]
    public void Token_IsUrlSafe()
    {
        // Arrange & Act
        var token = EmailVerificationToken.Create(Guid.NewGuid());

        // Assert - URL-safe Base64 should not contain +, /, or =
        token.Token.Should().NotContain("+");
        token.Token.Should().NotContain("/");
        token.Token.Should().NotContain("=");
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    public void Create_WithCustomExpiration_SetsCorrectExpirationTime(int expirationHours)
    {
        // Arrange & Act
        var token = EmailVerificationToken.Create(Guid.NewGuid(), expirationHours);

        // Assert
        var expectedExpiration = DateTime.UtcNow.AddHours(expirationHours);
        token.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }
}
