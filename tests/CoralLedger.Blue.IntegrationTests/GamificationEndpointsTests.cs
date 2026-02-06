using System.Net;
using System.Net.Http.Json;
using CoralLedger.Blue.Application.Features.Gamification.Queries.GetLeaderboard;
using CoralLedger.Blue.Application.Features.Gamification.Queries.GetUserProfile;
using CoralLedger.Blue.Application.Features.Gamification.Queries.GetUserAchievements;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardPoints;
using CoralLedger.Blue.Application.Features.Gamification.Commands.AwardBadge;
using CoralLedger.Blue.Web.Endpoints;
using FluentAssertions;

namespace CoralLedger.Blue.IntegrationTests;

public class GamificationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GamificationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUserProfile_WithNonExistentEmail_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@test.com";

        // Act
        var response = await _client.GetAsync($"/api/gamification/profile/{email}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLeaderboard_WithDefaultParameters_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/gamification/leaderboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var leaderboard = await response.Content.ReadFromJsonAsync<LeaderboardDto>();
        leaderboard.Should().NotBeNull();
        leaderboard!.Period.Should().Be(LeaderboardPeriod.AllTime);
    }

    [Fact]
    public async Task GetLeaderboard_WithWeeklyPeriod_ReturnsWeeklyLeaderboard()
    {
        // Act
        var response = await _client.GetAsync("/api/gamification/leaderboard?period=Weekly");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var leaderboard = await response.Content.ReadFromJsonAsync<LeaderboardDto>();
        leaderboard.Should().NotBeNull();
        leaderboard!.Period.Should().Be(LeaderboardPeriod.Weekly);
    }

    [Fact]
    public async Task GetLeaderboard_WithMonthlyPeriod_ReturnsMonthlyLeaderboard()
    {
        // Act
        var response = await _client.GetAsync("/api/gamification/leaderboard?period=Monthly");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var leaderboard = await response.Content.ReadFromJsonAsync<LeaderboardDto>();
        leaderboard.Should().NotBeNull();
        leaderboard!.Period.Should().Be(LeaderboardPeriod.Monthly);
    }

    [Fact]
    public async Task GetLeaderboard_WithPagination_ReturnsCorrectPage()
    {
        // Act
        var response = await _client.GetAsync("/api/gamification/leaderboard?pageSize=10&pageNumber=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var leaderboard = await response.Content.ReadFromJsonAsync<LeaderboardDto>();
        leaderboard.Should().NotBeNull();
        leaderboard!.Entries.Count.Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public async Task GetTopObservers_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/gamification/leaderboard/observers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetBadgeDefinitions_ReturnsAllBadgeTypes()
    {
        // Act
        var response = await _client.GetAsync("/api/gamification/badges");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var badges = await response.Content.ReadFromJsonAsync<List<BadgeDefinitionDto>>();
        badges.Should().NotBeNull();
        badges!.Count.Should().BeGreaterThan(0);
        
        // Verify badge structure
        var firstBadge = badges.First();
        firstBadge.Name.Should().NotBeNullOrEmpty();
        firstBadge.Description.Should().NotBeNullOrEmpty();
        firstBadge.Requirement.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetUserAchievements_WithNonExistentEmail_ReturnsEmptyList()
    {
        // Arrange
        var email = "nonexistent@test.com";

        // Act
        var response = await _client.GetAsync($"/api/gamification/achievements/{email}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var achievements = await response.Content.ReadFromJsonAsync<List<AchievementDto>>();
        achievements.Should().NotBeNull();
        achievements.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAchievements_WithCompletedOnlyFilter_ReturnsOk()
    {
        // Arrange
        var email = "test@test.com";

        // Act
        var response = await _client.GetAsync($"/api/gamification/achievements/{email}?completedOnly=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AwardPoints_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new AwardPointsRequest(
            Email: "test@test.com",
            Points: 100,
            Reason: "Test award");

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/gamification/points", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<AwardPointsResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.TotalPoints.Should().BeGreaterOrEqualTo(100);
    }

    [Fact]
    public async Task AwardBadge_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new AwardBadgeRequest(
            Email: "test2@test.com",
            BadgeType: "FirstObservation",
            Description: "Test badge award");

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/gamification/badges", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<AwardBadgeResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AwardBadge_WithInvalidBadgeType_ReturnsBadRequest()
    {
        // Arrange
        var request = new AwardBadgeRequest(
            Email: "test@test.com",
            BadgeType: "InvalidBadgeType",
            Description: "Test");

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/gamification/badges", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
