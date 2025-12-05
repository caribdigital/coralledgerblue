using CoralLedger.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Domain.Tests.ValueObjects;

public class AlertConditionTests
{
    #region BleachingCondition Tests

    [Fact]
    public void BleachingCondition_HasCorrectType()
    {
        // Arrange & Act
        var condition = new BleachingCondition();

        // Assert
        condition.Type.Should().Be("Bleaching");
    }

    [Fact]
    public void BleachingCondition_HasDefaultValues()
    {
        // Arrange & Act
        var condition = new BleachingCondition();

        // Assert
        condition.MinAlertLevel.Should().Be(2);
        condition.MinDegreeHeatingWeek.Should().BeNull();
        condition.MinSstAnomaly.Should().BeNull();
    }

    [Fact]
    public void BleachingCondition_CanSetCustomValues()
    {
        // Arrange & Act
        var condition = new BleachingCondition
        {
            MinAlertLevel = 3,
            MinDegreeHeatingWeek = 4.0,
            MinSstAnomaly = 1.5
        };

        // Assert
        condition.MinAlertLevel.Should().Be(3);
        condition.MinDegreeHeatingWeek.Should().Be(4.0);
        condition.MinSstAnomaly.Should().Be(1.5);
    }

    [Fact]
    public void BleachingCondition_RecordEquality_Works()
    {
        // Arrange
        var condition1 = new BleachingCondition { MinAlertLevel = 2, MinDegreeHeatingWeek = 4.0 };
        var condition2 = new BleachingCondition { MinAlertLevel = 2, MinDegreeHeatingWeek = 4.0 };
        var condition3 = new BleachingCondition { MinAlertLevel = 3, MinDegreeHeatingWeek = 4.0 };

        // Assert
        condition1.Should().Be(condition2);
        condition1.Should().NotBe(condition3);
    }

    #endregion

    #region FishingActivityCondition Tests

    [Fact]
    public void FishingActivityCondition_HasCorrectType()
    {
        // Arrange & Act
        var condition = new FishingActivityCondition();

        // Assert
        condition.Type.Should().Be("FishingActivity");
    }

    [Fact]
    public void FishingActivityCondition_HasDefaultValues()
    {
        // Arrange & Act
        var condition = new FishingActivityCondition();

        // Assert
        condition.MinEventCount.Should().Be(5);
        condition.TimeWindowHours.Should().Be(24);
        condition.OnlyInsideMpa.Should().BeTrue();
    }

    [Fact]
    public void FishingActivityCondition_CanSetCustomValues()
    {
        // Arrange & Act
        var condition = new FishingActivityCondition
        {
            MinEventCount = 10,
            TimeWindowHours = 48,
            OnlyInsideMpa = false
        };

        // Assert
        condition.MinEventCount.Should().Be(10);
        condition.TimeWindowHours.Should().Be(48);
        condition.OnlyInsideMpa.Should().BeFalse();
    }

    #endregion

    #region VesselInMpaCondition Tests

    [Fact]
    public void VesselInMpaCondition_HasCorrectType()
    {
        // Arrange & Act
        var condition = new VesselInMpaCondition();

        // Assert
        condition.Type.Should().Be("VesselInMPA");
    }

    [Fact]
    public void VesselInMpaCondition_HasDefaultValues()
    {
        // Arrange & Act
        var condition = new VesselInMpaCondition();

        // Assert
        condition.MinDurationMinutes.Should().Be(30);
        condition.OnlyFishingVessels.Should().BeTrue();
        condition.OnlyNoTakeZones.Should().BeFalse();
    }

    [Fact]
    public void VesselInMpaCondition_CanConfigureNoTakeZoneOnly()
    {
        // Arrange & Act - Configure for no-take zone monitoring
        var condition = new VesselInMpaCondition
        {
            MinDurationMinutes = 15,
            OnlyFishingVessels = true,
            OnlyNoTakeZones = true
        };

        // Assert
        condition.MinDurationMinutes.Should().Be(15);
        condition.OnlyNoTakeZones.Should().BeTrue();
    }

    #endregion

    #region VesselDarkCondition Tests

    [Fact]
    public void VesselDarkCondition_HasCorrectType()
    {
        // Arrange & Act
        var condition = new VesselDarkCondition();

        // Assert
        condition.Type.Should().Be("VesselDarkEvent");
    }

    [Fact]
    public void VesselDarkCondition_HasDefaultValues()
    {
        // Arrange & Act
        var condition = new VesselDarkCondition();

        // Assert
        condition.MinDarkDurationMinutes.Should().Be(60);
        condition.OnlyNearMpa.Should().BeTrue();
        condition.NearMpaDistanceKm.Should().Be(10);
    }

    [Fact]
    public void VesselDarkCondition_CanSetCustomDistance()
    {
        // Arrange & Act
        var condition = new VesselDarkCondition
        {
            NearMpaDistanceKm = 25.0,
            MinDarkDurationMinutes = 30
        };

        // Assert
        condition.NearMpaDistanceKm.Should().Be(25.0);
        condition.MinDarkDurationMinutes.Should().Be(30);
    }

    #endregion

    #region TemperatureCondition Tests

    [Fact]
    public void TemperatureCondition_HasCorrectType()
    {
        // Arrange & Act
        var condition = new TemperatureCondition();

        // Assert
        condition.Type.Should().Be("TemperatureAnomaly");
    }

    [Fact]
    public void TemperatureCondition_HasDefaultValues()
    {
        // Arrange & Act
        var condition = new TemperatureCondition();

        // Assert
        condition.MaxSst.Should().BeNull();
        condition.MaxSstAnomaly.Should().Be(1.0);
    }

    [Fact]
    public void TemperatureCondition_CanSetThresholds()
    {
        // Arrange & Act
        var condition = new TemperatureCondition
        {
            MaxSst = 30.0,
            MaxSstAnomaly = 2.0
        };

        // Assert
        condition.MaxSst.Should().Be(30.0);
        condition.MaxSstAnomaly.Should().Be(2.0);
    }

    #endregion

    #region CitizenObservationCondition Tests

    [Fact]
    public void CitizenObservationCondition_HasCorrectType()
    {
        // Arrange & Act
        var condition = new CitizenObservationCondition();

        // Assert
        condition.Type.Should().Be("CitizenObservation");
    }

    [Fact]
    public void CitizenObservationCondition_HasDefaultValues()
    {
        // Arrange & Act
        var condition = new CitizenObservationCondition();

        // Assert
        condition.MaxHealthStatus.Should().Be(2);
        condition.Keywords.Should().BeNull();
    }

    [Fact]
    public void CitizenObservationCondition_CanSetKeywords()
    {
        // Arrange & Act
        var condition = new CitizenObservationCondition
        {
            Keywords = "bleaching,dead,debris,pollution"
        };

        // Assert
        condition.Keywords.Should().Be("bleaching,dead,debris,pollution");
    }

    #endregion

    #region Polymorphism Tests

    [Fact]
    public void AlertCondition_CanBeStoredPolymorphically()
    {
        // Arrange
        var conditions = new List<AlertCondition>
        {
            new BleachingCondition { MinAlertLevel = 3 },
            new FishingActivityCondition { MinEventCount = 10 },
            new VesselInMpaCondition { MinDurationMinutes = 15 },
            new VesselDarkCondition { MinDarkDurationMinutes = 45 },
            new TemperatureCondition { MaxSstAnomaly = 1.5 },
            new CitizenObservationCondition { Keywords = "test" }
        };

        // Assert
        conditions.Should().HaveCount(6);
        conditions.Select(c => c.Type).Should().Contain(new[]
        {
            "Bleaching",
            "FishingActivity",
            "VesselInMPA",
            "VesselDarkEvent",
            "TemperatureAnomaly",
            "CitizenObservation"
        });
    }

    [Fact]
    public void AlertCondition_TypeDiscriminatorIsCorrect()
    {
        // This test ensures the Type property works as a discriminator
        AlertCondition condition = new BleachingCondition();

        // Assert
        condition.Type.Should().Be("Bleaching");

        condition = new FishingActivityCondition();
        condition.Type.Should().Be("FishingActivity");
    }

    #endregion
}
