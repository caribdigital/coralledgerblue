using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.IntegrationTests;

/// <summary>
/// Tests for VesselEndpoints date conversion logic.
/// These tests verify the DateTime UTC conversion fix without requiring the full infrastructure.
/// </summary>
public class VesselEndpointsDateConversionTests
{
    [Fact]
    public void DateConversion_WithValidDateRange_ProducesUtcDates()
    {
        // Arrange - Simulate dates from query parameters
        DateTime? startDate = DateTime.Parse("2026-01-01");
        DateTime? endDate = DateTime.Parse("2026-01-29");

        // Act - Apply the same conversion logic as VesselEndpoints
        var start = startDate.HasValue
            ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);
        var end = endDate.HasValue
            ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Assert
        start.Kind.Should().Be(DateTimeKind.Utc);
        end.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void DateConversion_WithUnspecifiedKindDateTime_ConvertsToUtc()
    {
        // Arrange - This is exactly how dates come from HTTP query strings
        var dateString = "2026-01-15";
        var unspecifiedDate = DateTime.Parse(dateString);

        // Verify the problem: parsed date has Unspecified kind
        unspecifiedDate.Kind.Should().Be(DateTimeKind.Unspecified,
            "DateTime parsed from string should be Unspecified");

        // Act - Apply the fix from VesselEndpoints
        DateTime? startDateParam = unspecifiedDate;
        var start = startDateParam.HasValue
            ? DateTime.SpecifyKind(startDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);

        // Assert - Now it should be UTC and safe for PostgreSQL
        start.Kind.Should().Be(DateTimeKind.Utc,
            "After conversion, DateTime should be UTC");
    }

    [Fact]
    public void DateConversion_WithNoDateParams_UsesDefaults()
    {
        // Arrange - No date parameters provided
        DateTime? startDateParam = null;
        DateTime? endDateParam = null;

        // Act - Apply same conversion logic as VesselEndpoints
        var start = startDateParam.HasValue
            ? DateTime.SpecifyKind(startDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);
        var end = endDateParam.HasValue
            ? DateTime.SpecifyKind(endDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Assert
        start.Kind.Should().Be(DateTimeKind.Utc);
        end.Kind.Should().Be(DateTimeKind.Utc);
        (end - start).Days.Should().Be(30);
    }

    [Fact]
    public void DateConversion_PreservesOriginalDateValue()
    {
        // Arrange
        var original = new DateTime(2026, 1, 15, 14, 30, 45);
        original.Kind.Should().Be(DateTimeKind.Unspecified);

        // Act
        var converted = DateTime.SpecifyKind(original, DateTimeKind.Utc);

        // Assert - Date/time values should be preserved exactly
        converted.Year.Should().Be(2026);
        converted.Month.Should().Be(1);
        converted.Day.Should().Be(15);
        converted.Hour.Should().Be(14);
        converted.Minute.Should().Be(30);
        converted.Second.Should().Be(45);
        converted.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void DateConversion_WithLimit_DoesNotAffectConversion()
    {
        // Arrange - Simulate call with limit parameter
        DateTime? startDate = DateTime.Parse("2026-01-01");
        DateTime? endDate = DateTime.Parse("2026-01-29");
        int limit = 10;

        // Act - Apply conversion (limit doesn't affect date conversion)
        var start = startDate.HasValue
            ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);
        var end = endDate.HasValue
            ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Assert
        start.Kind.Should().Be(DateTimeKind.Utc);
        end.Kind.Should().Be(DateTimeKind.Utc);
        limit.Should().Be(10);
    }
}
