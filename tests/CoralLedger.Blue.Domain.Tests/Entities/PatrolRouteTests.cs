using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Blue.Domain.Tests.Entities;

public class PatrolRouteTests
{
    private readonly GeometryFactory _geometryFactory;

    public PatrolRouteTests()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
    }

    [Fact]
    public void Create_ShouldCreatePatrolRouteWithDefaultValues()
    {
        // Arrange & Act
        var patrolRoute = PatrolRoute.Create();

        // Assert
        Assert.NotEqual(Guid.Empty, patrolRoute.Id);
        Assert.Equal(PatrolRouteStatus.InProgress, patrolRoute.Status);
        Assert.Equal(30, patrolRoute.RecordingIntervalSeconds);
        Assert.Null(patrolRoute.EndTime);
        Assert.Null(patrolRoute.TotalDistanceMeters);
        Assert.Null(patrolRoute.DurationSeconds);
    }

    [Fact]
    public void Create_WithInvalidRecordingInterval_ShouldThrowException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            PatrolRoute.Create(recordingIntervalSeconds: 3)); // Less than 5
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            PatrolRoute.Create(recordingIntervalSeconds: 400)); // More than 300
    }

    [Fact]
    public void AddPoint_WhenInProgress_ShouldAddPointSuccessfully()
    {
        // Arrange
        var patrolRoute = PatrolRoute.Create();
        var location = _geometryFactory.CreatePoint(new Coordinate(-77.5, 24.5));
        var point = PatrolRoutePoint.Create(
            patrolRoute.Id,
            location,
            DateTime.UtcNow);

        // Act
        patrolRoute.AddPoint(point);

        // Assert
        Assert.Single(patrolRoute.Points);
        Assert.Contains(point, patrolRoute.Points);
    }

    [Fact]
    public void AddPoint_WhenCompleted_ShouldThrowException()
    {
        // Arrange
        var patrolRoute = PatrolRoute.Create();
        patrolRoute.Complete();
        var location = _geometryFactory.CreatePoint(new Coordinate(-77.5, 24.5));
        var point = PatrolRoutePoint.Create(
            patrolRoute.Id,
            location,
            DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => patrolRoute.AddPoint(point));
    }

    [Fact]
    public void AddWaypoint_WhenInProgress_ShouldAddWaypointSuccessfully()
    {
        // Arrange
        var patrolRoute = PatrolRoute.Create();
        var location = _geometryFactory.CreatePoint(new Coordinate(-77.5, 24.5));
        var waypoint = PatrolWaypoint.Create(
            patrolRoute.Id,
            location,
            DateTime.UtcNow,
            "Test Waypoint");

        // Act
        patrolRoute.AddWaypoint(waypoint);

        // Assert
        Assert.Single(patrolRoute.Waypoints);
        Assert.Contains(waypoint, patrolRoute.Waypoints);
    }

    [Fact]
    public void Complete_WhenInProgress_ShouldCompleteSuccessfully()
    {
        // Arrange
        var patrolRoute = PatrolRoute.Create(officerName: "Test Officer");
        var startTime = patrolRoute.StartTime;
        
        // Add some points
        for (int i = 0; i < 3; i++)
        {
            var location = _geometryFactory.CreatePoint(new Coordinate(-77.5 + i * 0.01, 24.5 + i * 0.01));
            var point = PatrolRoutePoint.Create(
                patrolRoute.Id,
                location,
                DateTime.UtcNow.AddMinutes(i * 5));
            patrolRoute.AddPoint(point);
        }

        // Act
        patrolRoute.Complete("Patrol completed successfully");

        // Assert
        Assert.Equal(PatrolRouteStatus.Completed, patrolRoute.Status);
        Assert.NotNull(patrolRoute.EndTime);
        Assert.True(patrolRoute.EndTime > startTime);
        Assert.NotNull(patrolRoute.DurationSeconds);
        Assert.NotNull(patrolRoute.TotalDistanceMeters);
        Assert.True(patrolRoute.TotalDistanceMeters > 0);
        Assert.NotNull(patrolRoute.RouteGeometry);
        Assert.Equal(3, patrolRoute.RouteGeometry.NumPoints);
    }

    [Fact]
    public void Cancel_WhenInProgress_ShouldCancelSuccessfully()
    {
        // Arrange
        var patrolRoute = PatrolRoute.Create();

        // Act
        patrolRoute.Cancel("Equipment failure");

        // Assert
        Assert.Equal(PatrolRouteStatus.Cancelled, patrolRoute.Status);
        Assert.NotNull(patrolRoute.EndTime);
        Assert.Contains("Equipment failure", patrolRoute.Notes);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ShouldThrowException()
    {
        // Arrange
        var patrolRoute = PatrolRoute.Create();
        patrolRoute.Complete();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => patrolRoute.Complete());
    }
}
