using System.Net;
using System.Text.Json;

namespace CoralLedger.Aspire.Tests.Tests;

/// <summary>
/// Tests to verify database seeding completed successfully
/// </summary>
[Collection("Aspire")]
public class DatabaseSeedingTests
{
    private readonly AspireIntegrationFixture _fixture;

    public DatabaseSeedingTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Database_ContainsBahamasMpas()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/mpas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var mpas = JsonSerializer.Deserialize<JsonElement>(content);

        // Should have Bahamas MPAs seeded
        mpas.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Database_ContainsBahamianSpecies()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/species");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var species = JsonSerializer.Deserialize<JsonElement>(content);

        // Should have Bahamian species seeded
        species.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HealthCheck_DatabaseIsHealthy()
    {
        // Act
        var response = await _fixture.WebClient.GetAsync("/api/diagnostics/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("database");
    }
}
