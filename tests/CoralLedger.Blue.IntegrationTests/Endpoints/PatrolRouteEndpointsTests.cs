using System.Net;
using System.Net.Http.Json;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.StartPatrolRoute;
using CoralLedger.Blue.Application.Features.PatrolRoutes.Commands.StopPatrolRoute;
using CoralLedger.Blue.Application.Features.PatrolRoutes.DTOs;
using Xunit;

namespace CoralLedger.Blue.IntegrationTests.Endpoints;

public class PatrolRouteEndpointsTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PatrolRouteEndpointsTests(IntegrationTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task StartPatrolRoute_ShouldReturnCreated()
    {
        // Arrange
        var request = new
        {
            OfficerName = "Test Officer",
            OfficerId = "OFF123",
            Notes = "Test patrol",
            RecordingIntervalSeconds = 30
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/patrols/start", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<StartPatrolRouteResult>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.PatrolRouteId);
    }

    [Fact]
    public async Task GetPatrolRoutes_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/patrols");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<List<PatrolRouteSummaryDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task PatrolRouteWorkflow_ShouldWorkEndToEnd()
    {
        // 1. Start patrol
        var startRequest = new
        {
            OfficerName = "Integration Test Officer",
            OfficerId = "INT001",
            Notes = "Integration test patrol",
            RecordingIntervalSeconds = 30
        };

        var startResponse = await _client.PostAsJsonAsync("/api/patrols/start", startRequest);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        
        var startResult = await startResponse.Content.ReadFromJsonAsync<StartPatrolRouteResult>();
        Assert.NotNull(startResult);
        var patrolId = startResult.PatrolRouteId!.Value;

        // 2. Add GPS points
        var addPointRequest = new
        {
            Longitude = -77.5,
            Latitude = 24.5,
            Accuracy = 10.0,
            Speed = 5.0
        };

        var pointResponse = await _client.PostAsJsonAsync($"/api/patrols/{patrolId}/points", addPointRequest);
        Assert.Equal(HttpStatusCode.Created, pointResponse.StatusCode);

        // Add another point
        var addPointRequest2 = new
        {
            Longitude = -77.51,
            Latitude = 24.51,
            Accuracy = 10.0,
            Speed = 5.0
        };

        var pointResponse2 = await _client.PostAsJsonAsync($"/api/patrols/{patrolId}/points", addPointRequest2);
        Assert.Equal(HttpStatusCode.Created, pointResponse2.StatusCode);

        // 3. Add a waypoint
        var addWaypointRequest = new
        {
            Longitude = -77.505,
            Latitude = 24.505,
            Title = "Test Waypoint",
            Notes = "Found something interesting",
            WaypointType = "Observation"
        };

        var waypointResponse = await _client.PostAsJsonAsync($"/api/patrols/{patrolId}/waypoints", addWaypointRequest);
        Assert.Equal(HttpStatusCode.Created, waypointResponse.StatusCode);

        // 4. Get patrol details
        var getResponse = await _client.GetAsync($"/api/patrols/{patrolId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        
        var patrol = await getResponse.Content.ReadFromJsonAsync<PatrolRouteDetailDto>();
        Assert.NotNull(patrol);
        Assert.Equal("Integration Test Officer", patrol.OfficerName);
        Assert.Equal(2, patrol.Points.Count);
        Assert.Single(patrol.Waypoints);

        // 5. Stop patrol
        var stopRequest = new
        {
            CompletionNotes = "Patrol completed successfully"
        };

        var stopResponse = await _client.PostAsJsonAsync($"/api/patrols/{patrolId}/stop", stopRequest);
        Assert.Equal(HttpStatusCode.OK, stopResponse.StatusCode);
        
        var stopResult = await stopResponse.Content.ReadFromJsonAsync<StopPatrolRouteResult>();
        Assert.NotNull(stopResult);
        Assert.True(stopResult.Success);
        Assert.Equal("Completed", stopResult.Status);

        // 6. Get GeoJSON
        var geoJsonResponse = await _client.GetAsync($"/api/patrols/{patrolId}/geojson");
        Assert.Equal(HttpStatusCode.OK, geoJsonResponse.StatusCode);
    }
}
