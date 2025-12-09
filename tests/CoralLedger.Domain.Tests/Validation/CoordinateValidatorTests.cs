using CoralLedger.Domain.Validation;
using FluentAssertions;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Domain.Tests.Validation;

public class CoordinateValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(-45)]
    [InlineData(90)]
    [InlineData(-90)]
    public void IsValidLatitude_ValidValues_ReturnsTrue(double latitude)
    {
        CoordinateValidator.IsValidLatitude(latitude).Should().BeTrue();
    }

    [Theory]
    [InlineData(91)]
    [InlineData(-91)]
    [InlineData(180)]
    [InlineData(-180)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsValidLatitude_InvalidValues_ReturnsFalse(double latitude)
    {
        CoordinateValidator.IsValidLatitude(latitude).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(180)]
    [InlineData(-180)]
    public void IsValidLongitude_ValidValues_ReturnsTrue(double longitude)
    {
        CoordinateValidator.IsValidLongitude(longitude).Should().BeTrue();
    }

    [Theory]
    [InlineData(181)]
    [InlineData(-181)]
    [InlineData(360)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsValidLongitude_InvalidValues_ReturnsFalse(double longitude)
    {
        CoordinateValidator.IsValidLongitude(longitude).Should().BeFalse();
    }

    [Theory]
    [InlineData(25.0, -77.5)] // Nassau, Bahamas
    [InlineData(0, 0)]        // Null Island
    [InlineData(90, 180)]     // Edge case
    [InlineData(-90, -180)]   // Edge case
    public void IsValidCoordinate_ValidCoordinates_ReturnsTrue(double lat, double lon)
    {
        CoordinateValidator.IsValidCoordinate(lat, lon).Should().BeTrue();
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    public void IsValidCoordinate_InvalidCoordinates_ReturnsFalse(double lat, double lon)
    {
        CoordinateValidator.IsValidCoordinate(lat, lon).Should().BeFalse();
    }

    [Fact]
    public void IsValidPoint_ValidPoint_ReturnsTrue()
    {
        var point = new Point(-77.35, 25.05); // Nassau (lon, lat)
        CoordinateValidator.IsValidPoint(point).Should().BeTrue();
    }

    [Fact]
    public void IsValidPoint_NullPoint_ReturnsFalse()
    {
        CoordinateValidator.IsValidPoint(null).Should().BeFalse();
    }

    [Fact]
    public void IsValidPoint_EmptyPoint_ReturnsFalse()
    {
        var point = Point.Empty;
        CoordinateValidator.IsValidPoint(point).Should().BeFalse();
    }

    [Theory]
    [InlineData(25.05, -77.35)]  // Nassau
    [InlineData(24.97, -77.53)]  // Paradise Island
    [InlineData(26.54, -78.70)]  // Grand Bahama
    [InlineData(23.05, -75.75)]  // Long Island
    [InlineData(21.10, -73.50)]  // Great Inagua
    public void IsWithinBahamas_BahamianCoordinates_ReturnsTrue(double lat, double lon)
    {
        CoordinateValidator.IsWithinBahamas(lat, lon).Should().BeTrue();
    }

    [Theory]
    [InlineData(25.78, -81.00)]  // West of Bahamas (too far west)
    [InlineData(18.47, -69.90)]  // Santo Domingo (too far south)
    [InlineData(32.29, -64.78)]  // Bermuda (too far north)
    [InlineData(0, 0)]           // Null Island (Africa)
    [InlineData(25.78, -72.00)]  // East of Bahamas (too far east)
    public void IsWithinBahamas_NonBahamianCoordinates_ReturnsFalse(double lat, double lon)
    {
        CoordinateValidator.IsWithinBahamas(lat, lon).Should().BeFalse();
    }

    [Fact]
    public void IsWithinBahamas_ValidPoint_ReturnsTrue()
    {
        var point = new Point(-77.35, 25.05); // Nassau
        CoordinateValidator.IsWithinBahamas(point).Should().BeTrue();
    }

    [Fact]
    public void IsWithinBahamas_NullPoint_ReturnsFalse()
    {
        CoordinateValidator.IsWithinBahamas(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(25.123456)]
    [InlineData(25.1)]
    [InlineData(25)]
    public void HasValidPrecision_ValidPrecision_ReturnsTrue(double value)
    {
        CoordinateValidator.HasValidPrecision(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(25.1234567)]
    [InlineData(25.12345678)]
    [InlineData(25.123456789012)]
    public void HasValidPrecision_TooMuchPrecision_ReturnsFalse(double value)
    {
        CoordinateValidator.HasValidPrecision(value).Should().BeFalse();
    }

    [Theory]
    [InlineData(25.123456789, 25.123456)]
    [InlineData(25.1234, 25.1234)]
    [InlineData(-77.12345678901234, -77.123456)]
    public void TruncatePrecision_TruncatesToMaxPrecision(double input, double expected)
    {
        var result = CoordinateValidator.TruncatePrecision(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Validate_ValidCoordinate_ReturnsSuccess()
    {
        var result = CoordinateValidator.Validate(25.05, -77.35);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_InvalidLatitude_ReturnsError()
    {
        var result = CoordinateValidator.Validate(91, -77.35);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Latitude") && e.Contains("outside valid range"));
    }

    [Fact]
    public void Validate_InvalidLongitude_ReturnsError()
    {
        var result = CoordinateValidator.Validate(25.05, 200);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Longitude") && e.Contains("outside valid range"));
    }

    [Fact]
    public void Validate_NaNLatitude_ReturnsError()
    {
        var result = CoordinateValidator.Validate(double.NaN, -77.35);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Latitude is NaN");
    }

    [Fact]
    public void Validate_InfinityLongitude_ReturnsError()
    {
        var result = CoordinateValidator.Validate(25.05, double.PositiveInfinity);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Longitude is infinite");
    }

    [Fact]
    public void Validate_RequireBahamas_ValidBahamianCoordinate_ReturnsSuccess()
    {
        var result = CoordinateValidator.Validate(25.05, -77.35, requireBahamas: true);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequireBahamas_NonBahamianCoordinate_ReturnsError()
    {
        var result = CoordinateValidator.Validate(25.78, -81.00, requireBahamas: true); // West of Bahamas

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("outside The Bahamas"));
    }

    [Fact]
    public void ValidatePoint_NullPoint_ReturnsError()
    {
        var result = CoordinateValidator.Validate(null);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Point is null");
    }

    [Fact]
    public void ValidatePoint_EmptyPoint_ReturnsError()
    {
        var result = CoordinateValidator.Validate(Point.Empty);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Point is empty");
    }

    [Fact]
    public void ValidatePoint_ValidPoint_ReturnsSuccess()
    {
        var point = new Point(-77.35, 25.05);
        var result = CoordinateValidator.Validate(point);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(190, -170)]
    [InlineData(-190, 170)]
    [InlineData(360, 0)]
    [InlineData(540, 180)]
    public void NormalizeLongitude_OutOfRangeLongitude_NormalizesToValidRange(double input, double expected)
    {
        var result = CoordinateValidator.NormalizeLongitude(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(-90, -90)]
    [InlineData(180, 180)]
    [InlineData(-180, -180)]
    public void NormalizeLongitude_ValidLongitude_ReturnsUnchanged(double input, double expected)
    {
        var result = CoordinateValidator.NormalizeLongitude(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void CoordinateValidationResult_ErrorMessage_JoinsErrors()
    {
        var result = new CoordinateValidationResult(false, new[] { "Error 1", "Error 2" });
        result.ErrorMessage.Should().Be("Error 1; Error 2");
    }

    [Fact]
    public void CoordinateValidationResult_Success_HasNoErrors()
    {
        var result = CoordinateValidationResult.Success;
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void BahamasBounds_ContainsCorrectValues()
    {
        CoordinateValidator.BahamasBounds.MinLatitude.Should().BeApproximately(20.91, 0.01);
        CoordinateValidator.BahamasBounds.MaxLatitude.Should().BeApproximately(27.26, 0.01);
        CoordinateValidator.BahamasBounds.MinLongitude.Should().BeApproximately(-80.47, 0.01);
        CoordinateValidator.BahamasBounds.MaxLongitude.Should().BeApproximately(-72.71, 0.01);
    }
}
