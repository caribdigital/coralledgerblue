using FluentAssertions;
using Xunit;

namespace CoralLedger.Blue.Application.Tests.Utilities;

/// <summary>
/// Unit tests verifying DateTime UTC conversion behavior.
/// These tests document the fix for the fishing events API DateTime handling issue.
///
/// Root Cause: When DateTime parameters come from HTTP query strings without timezone info,
/// they are parsed with Kind=Unspecified. PostgreSQL's Npgsql driver rejects Unspecified
/// DateTimes for timestamp with time zone columns, throwing ArgumentException.
///
/// Fix: Use DateTime.SpecifyKind to convert Unspecified to UTC before database queries.
/// </summary>
public class DateTimeUtcConversionTests
{
    [Fact]
    public void DateTime_ParsedFromString_HasUnspecifiedKind()
    {
        // Arrange - This simulates how dates come from HTTP query strings
        var dateString = "2026-01-15";

        // Act
        var parsed = DateTime.Parse(dateString);

        // Assert - Confirms the root cause: parsed dates are Unspecified
        parsed.Kind.Should().Be(DateTimeKind.Unspecified,
            "DateTime parsed from string without timezone info should be Unspecified");
    }

    [Fact]
    public void DateTime_UtcNow_HasUtcKind()
    {
        // Act
        var utcNow = DateTime.UtcNow;

        // Assert
        utcNow.Kind.Should().Be(DateTimeKind.Utc,
            "DateTime.UtcNow should have UTC kind");
    }

    [Fact]
    public void DateTime_SpecifyKind_ConvertsUnspecifiedToUtc()
    {
        // Arrange - Simulate query string date
        var unspecifiedDate = DateTime.Parse("2026-01-15T10:30:00");
        unspecifiedDate.Kind.Should().Be(DateTimeKind.Unspecified);

        // Act - This is the fix applied in VesselEndpoints
        var utcDate = DateTime.SpecifyKind(unspecifiedDate, DateTimeKind.Utc);

        // Assert
        utcDate.Kind.Should().Be(DateTimeKind.Utc,
            "SpecifyKind should convert to UTC kind");
        utcDate.Should().Be(unspecifiedDate,
            "The actual date/time value should remain unchanged");
    }

    [Fact]
    public void DateTime_SpecifyKind_PreservesDateTimeValue()
    {
        // Arrange
        var original = new DateTime(2026, 1, 15, 14, 30, 45);

        // Act
        var converted = DateTime.SpecifyKind(original, DateTimeKind.Utc);

        // Assert
        converted.Year.Should().Be(2026);
        converted.Month.Should().Be(1);
        converted.Day.Should().Be(15);
        converted.Hour.Should().Be(14);
        converted.Minute.Should().Be(30);
        converted.Second.Should().Be(45);
    }

    [Fact]
    public void FishingEventsDateConversion_SimulatesEndpointLogic()
    {
        // Arrange - Simulate nullable DateTime? from query parameter
        DateTime? startDateParam = DateTime.Parse("2026-01-01");
        DateTime? endDateParam = DateTime.Parse("2026-01-29");

        // Act - Replicate the exact fix from VesselEndpoints.cs
        var start = startDateParam.HasValue
            ? DateTime.SpecifyKind(startDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);
        var end = endDateParam.HasValue
            ? DateTime.SpecifyKind(endDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Assert
        start.Kind.Should().Be(DateTimeKind.Utc,
            "Start date should be UTC after conversion");
        end.Kind.Should().Be(DateTimeKind.Utc,
            "End date should be UTC after conversion");
    }

    [Fact]
    public void FishingEventsDateConversion_NullParametersUseDefaults()
    {
        // Arrange - Simulate null parameters (no query string values)
        DateTime? startDateParam = null;
        DateTime? endDateParam = null;

        // Act - Replicate the exact fix from VesselEndpoints.cs
        var start = startDateParam.HasValue
            ? DateTime.SpecifyKind(startDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);
        var end = endDateParam.HasValue
            ? DateTime.SpecifyKind(endDateParam.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Assert
        start.Kind.Should().Be(DateTimeKind.Utc,
            "Default start date should be UTC");
        end.Kind.Should().Be(DateTimeKind.Utc,
            "Default end date should be UTC");
        (end - start).Days.Should().Be(30,
            "Default range should be 30 days");
    }
}
