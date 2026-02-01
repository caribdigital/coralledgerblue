using CoralLedger.Blue.Domain.Entities;
using System;
using CoralLedger.Blue.Domain.Enums;
using FluentAssertions;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class MarineProtectedAreaTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private static Polygon CreateTestPolygon(double centerLat = 24.5, double centerLon = -77.5, double size = 0.1)
    {
        var coordinates = new[]
        {
            new Coordinate(centerLon - size, centerLat - size),
            new Coordinate(centerLon + size, centerLat - size),
            new Coordinate(centerLon + size, centerLat + size),
            new Coordinate(centerLon - size, centerLat + size),
            new Coordinate(centerLon - size, centerLat - size) // Close the ring
        };
        return GeometryFactory.CreatePolygon(coordinates);
    }

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var name = "Test Marine Protected Area";
        var boundary = CreateTestPolygon();

        // Act
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            name,
            boundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        // Assert
        mpa.Name.Should().Be(name);
        mpa.Boundary.Should().Be(boundary);
        mpa.ProtectionLevel.Should().Be(ProtectionLevel.NoTake);
        mpa.IslandGroup.Should().Be(IslandGroup.Exumas);
        mpa.Status.Should().Be(MpaStatus.Active);
        mpa.Id.Should().NotBeEmpty();
        mpa.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithOptionalParameters_SetsOptionalProperties()
    {
        // Arrange
        var name = "Exuma Cays Land and Sea Park";
        var boundary = CreateTestPolygon();
        var wdpaId = "305071";
        var description = "The first land-and-sea park in the world";
        var managingAuthority = "Bahamas National Trust";
        var designationDate = new DateOnly(1958, 1, 1);

        // Act
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            name,
            boundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas,
            wdpaId: wdpaId,
            description: description,
            managingAuthority: managingAuthority,
            designationDate: designationDate);

        // Assert
        mpa.WdpaId.Should().Be(wdpaId);
        mpa.Description.Should().Be(description);
        mpa.ManagingAuthority.Should().Be(managingAuthority);
        mpa.DesignationDate.Should().Be(designationDate);
    }

    [Fact]
    public void Create_CalculatesCentroidFromBoundary()
    {
        // Arrange
        var boundary = CreateTestPolygon(centerLat: 24.5, centerLon: -77.5, size: 0.1);

        // Act
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            ProtectionLevel.HighlyProtected,
            IslandGroup.NewProvidence);

        // Assert
        mpa.Centroid.Should().NotBeNull();
        mpa.Centroid.X.Should().BeApproximately(-77.5, 0.01);
        mpa.Centroid.Y.Should().BeApproximately(24.5, 0.01);
    }

    [Fact]
    public void Create_CalculatesAreaInSquareKm()
    {
        // Arrange
        var boundary = CreateTestPolygon(size: 0.1); // 0.2 x 0.2 degree square

        // Act
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            ProtectionLevel.LightlyProtected,
            IslandGroup.NewProvidence);

        // Assert
        // Area should be positive and reasonable for the given polygon
        mpa.AreaSquareKm.Should().BeGreaterThan(0);
        // A 0.2 x 0.2 degree square at 24.5N is approximately (0.2*111)^2 ≈ 493 km²
        // But the formula uses a simple approximation, so we just check it's in a reasonable range
        mpa.AreaSquareKm.Should().BeInRange(100, 1000);
    }

    [Fact]
    public void UpdateDescription_UpdatesDescriptionAndModifiedAt()
    {
        // Arrange
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            CreateTestPolygon(),
            ProtectionLevel.NoTake,
            IslandGroup.Abaco);
        var originalModifiedAt = mpa.ModifiedAt;
        var newDescription = "Updated description for the marine protected area";

        // Act
        mpa.UpdateDescription(newDescription);

        // Assert
        mpa.Description.Should().Be(newDescription);
        mpa.ModifiedAt.Should().NotBeNull();
        mpa.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        mpa.ModifiedAt.Should().NotBe(originalModifiedAt);
    }

    [Fact]
    public void UpdateProtectionLevel_UpdatesLevelAndModifiedAt()
    {
        // Arrange
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            CreateTestPolygon(),
            ProtectionLevel.LightlyProtected,
            IslandGroup.GrandBahama);

        // Act
        mpa.UpdateProtectionLevel(ProtectionLevel.NoTake);

        // Assert
        mpa.ProtectionLevel.Should().Be(ProtectionLevel.NoTake);
        mpa.ModifiedAt.Should().NotBeNull();
        mpa.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Decommission_SetsStatusToDecommissioned()
    {
        // Arrange
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            CreateTestPolygon(),
            ProtectionLevel.HighlyProtected,
            IslandGroup.Inagua);
        mpa.Status.Should().Be(MpaStatus.Active);

        // Act
        mpa.Decommission();

        // Assert
        mpa.Status.Should().Be(MpaStatus.Decommissioned);
        mpa.ModifiedAt.Should().NotBeNull();
        mpa.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(ProtectionLevel.NoTake)]
    [InlineData(ProtectionLevel.HighlyProtected)]
    [InlineData(ProtectionLevel.LightlyProtected)]
    [InlineData(ProtectionLevel.MinimalProtection)]
    public void Create_SupportsAllProtectionLevels(ProtectionLevel protectionLevel)
    {
        // Arrange
        var boundary = CreateTestPolygon();

        // Act
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            protectionLevel,
            IslandGroup.Exumas);

        // Assert
        mpa.ProtectionLevel.Should().Be(protectionLevel);
    }

    [Theory]
    [InlineData(IslandGroup.NewProvidence)]
    [InlineData(IslandGroup.Exumas)]
    [InlineData(IslandGroup.Andros)]
    [InlineData(IslandGroup.Abaco)]
    [InlineData(IslandGroup.GrandBahama)]
    [InlineData(IslandGroup.Inagua)]
    [InlineData(IslandGroup.LongIsland)]
    public void Create_SupportsVariousIslandGroups(IslandGroup islandGroup)
    {
        // Arrange
        var boundary = CreateTestPolygon();

        // Act
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            ProtectionLevel.NoTake,
            islandGroup);

        // Assert
        mpa.IslandGroup.Should().Be(islandGroup);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var boundary = CreateTestPolygon();

        // Act
        var mpa1 = MarineProtectedArea.Create(Guid.NewGuid(), "MPA 1", boundary, ProtectionLevel.NoTake, IslandGroup.Exumas);
        var mpa2 = MarineProtectedArea.Create(Guid.NewGuid(), "MPA 2", boundary, ProtectionLevel.NoTake, IslandGroup.Exumas);

        // Assert
        mpa1.Id.Should().NotBe(mpa2.Id);
    }

    [Fact]
    public void UpdateBoundaryFromWdpa_UpdatesBoundaryAndRelatedFields()
    {
        // Arrange
        var originalBoundary = CreateTestPolygon(centerLat: 24.0, centerLon: -77.0, size: 0.1);
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            originalBoundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var newBoundary = CreateTestPolygon(centerLat: 25.0, centerLon: -78.0, size: 0.2);

        // Act
        mpa.UpdateBoundaryFromWdpa(newBoundary);

        // Assert
        mpa.Boundary.Should().Be(newBoundary);
        mpa.Centroid.X.Should().BeApproximately(-78.0, 0.01);
        mpa.Centroid.Y.Should().BeApproximately(25.0, 0.01);
        mpa.WdpaLastSync.Should().NotBeNull();
        mpa.WdpaLastSync.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        mpa.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateBoundaryFromWdpa_RecalculatesArea()
    {
        // Arrange
        var smallBoundary = CreateTestPolygon(size: 0.05);
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            smallBoundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);
        var originalArea = mpa.AreaSquareKm;

        var largerBoundary = CreateTestPolygon(size: 0.2);

        // Act
        mpa.UpdateBoundaryFromWdpa(largerBoundary);

        // Assert
        mpa.AreaSquareKm.Should().BeGreaterThan(originalArea);
    }

    [Fact]
    public void SetSimplifiedBoundaries_SetsSimplifiedGeometries()
    {
        // Arrange
        var boundary = CreateTestPolygon();
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var detailSimplified = CreateTestPolygon(size: 0.09);
        var mediumSimplified = CreateTestPolygon(size: 0.08);
        var lowSimplified = CreateTestPolygon(size: 0.05);

        // Act
        mpa.SetSimplifiedBoundaries(detailSimplified, mediumSimplified, lowSimplified);

        // Assert
        mpa.BoundarySimplifiedDetail.Should().Be(detailSimplified);
        mpa.BoundarySimplifiedMedium.Should().Be(mediumSimplified);
        mpa.BoundarySimplifiedLow.Should().Be(lowSimplified);
        mpa.ModifiedAt.Should().NotBeNull();
        mpa.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SetSimplifiedBoundaries_AllowsNullValues()
    {
        // Arrange
        var boundary = CreateTestPolygon();
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        // Act
        mpa.SetSimplifiedBoundaries(null, null, null);

        // Assert
        mpa.BoundarySimplifiedDetail.Should().BeNull();
        mpa.BoundarySimplifiedMedium.Should().BeNull();
        mpa.BoundarySimplifiedLow.Should().BeNull();
        mpa.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetWdpaId_SetsWdpaId()
    {
        // Arrange
        var boundary = CreateTestPolygon();
        var mpa = MarineProtectedArea.Create(Guid.NewGuid(), 
            "Test MPA",
            boundary,
            ProtectionLevel.NoTake,
            IslandGroup.Exumas);

        var wdpaId = "305071";

        // Act
        mpa.SetWdpaId(wdpaId);

        // Assert
        mpa.WdpaId.Should().Be(wdpaId);
        mpa.ModifiedAt.Should().NotBeNull();
        mpa.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
