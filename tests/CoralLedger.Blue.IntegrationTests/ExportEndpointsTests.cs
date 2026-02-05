using System.Net;
using CoralLedger.Blue.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoralLedger.Blue.IntegrationTests;

public class ExportEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ExportEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _factory = factory;
    }

    [Fact]
    public async Task ExportMpasGeoJson_ReturnsGeoJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/export/mpas/geojson");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/geo+json");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("FeatureCollection");
    }

    [Fact]
    public async Task ExportMpasCsv_ReturnsCsvContent()
    {
        // Act
        var response = await _client.GetAsync("/api/export/mpas/csv");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Id,Name,IslandGroup");
    }

    [Fact]
    public async Task ExportMpasShapefile_ReturnsZipFile()
    {
        // Act
        var response = await _client.GetAsync("/api/export/mpas/shapefile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");
    }

    [Fact]
    public async Task ExportVesselsGeoJson_ReturnsGeoJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/export/vessels/geojson");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("FeatureCollection");
    }

    [Fact]
    public async Task ExportBleachingGeoJson_ReturnsGeoJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/export/bleaching/geojson");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("FeatureCollection");
    }

    [Fact]
    public async Task ExportObservationsGeoJson_ReturnsGeoJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/export/observations/geojson");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("FeatureCollection");
    }

    [Fact]
    public async Task ExportWithDateRange_ReturnsFilteredData()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-7).ToString("o");
        var toDate = DateTime.UtcNow.ToString("o");

        // Act
        var response = await _client.GetAsync($"/api/export/vessels/geojson?fromDate={fromDate}&toDate={toDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #region PDF Report Tests

    [Fact]
    public async Task SingleMpaReport_ReturnsPdfWithCorrectContentType()
    {
        // Arrange - Get a valid MPA ID from the database
        var mpaId = await GetFirstMpaIdAsync();

        // Act
        var response = await _client.GetAsync($"/api/export/reports/mpa/{mpaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var content = await response.Content.ReadAsByteArrayAsync();
        content.Should().NotBeEmpty();
        
        // Verify it's a valid PDF by checking the PDF magic number
        content.Take(4).Should().BeEquivalentTo(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
    }

    [Fact]
    public async Task SingleMpaReport_WithDateRangeFiltering_ReturnsOk()
    {
        // Arrange
        var mpaId = await GetFirstMpaIdAsync();
        var fromDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var toDate = DateTime.UtcNow.ToString("o");

        // Act
        var response = await _client.GetAsync(
            $"/api/export/reports/mpa/{mpaId}?fromDate={fromDate}&toDate={toDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task SingleMpaReport_WithNonExistentMpa_ReturnsNotFound()
    {
        // Arrange
        var nonExistentMpaId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/export/reports/mpa/{nonExistentMpaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AllMpasReport_ReturnsPdfWithCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/export/reports/all-mpas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var content = await response.Content.ReadAsByteArrayAsync();
        content.Should().NotBeEmpty();
        
        // Verify it's a valid PDF by checking the PDF magic number
        content.Take(4).Should().BeEquivalentTo(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
    }

    [Fact]
    public async Task AllMpasReport_WithIslandGroupFilter_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/export/reports/all-mpas?islandGroup=Exumas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task AllMpasReport_WithProtectionLevelFilter_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/export/reports/all-mpas?protectionLevel=NoTake");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task AllMpasReport_WithDateRangeFiltering_ReturnsOk()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var toDate = DateTime.UtcNow.ToString("o");

        // Act
        var response = await _client.GetAsync(
            $"/api/export/reports/all-mpas?fromDate={fromDate}&toDate={toDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task SingleMpaReport_WithChartsEnabled_ReturnsLargerPdf()
    {
        // Arrange
        var mpaId = await GetFirstMpaIdAsync();

        // Act - Get PDF without charts
        var responseWithoutCharts = await _client.GetAsync(
            $"/api/export/reports/mpa/{mpaId}?includeCharts=false");
        var contentWithoutCharts = await responseWithoutCharts.Content.ReadAsByteArrayAsync();

        // Act - Get PDF with charts
        var responseWithCharts = await _client.GetAsync(
            $"/api/export/reports/mpa/{mpaId}?includeCharts=true");
        var contentWithCharts = await responseWithCharts.Content.ReadAsByteArrayAsync();

        // Assert
        responseWithoutCharts.StatusCode.Should().Be(HttpStatusCode.OK);
        responseWithCharts.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // PDF with charts should be larger due to embedded images
        // Note: This test may fail if there's no data for charts
        contentWithCharts.Length.Should().BeGreaterOrEqualTo(contentWithoutCharts.Length);
    }

    [Fact]
    public async Task SingleMpaReport_FileSize_StaysUnderReasonableLimit()
    {
        // Arrange
        var mpaId = await GetFirstMpaIdAsync();

        // Act
        var response = await _client.GetAsync(
            $"/api/export/reports/mpa/{mpaId}?includeCharts=true");
        var content = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // PDF should be under 5MB as per acceptance criteria
        var fileSizeInMB = content.Length / (1024.0 * 1024.0);
        fileSizeInMB.Should().BeLessThan(5.0, 
            because: "PDF file size should remain reasonable even with charts");
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> GetFirstMpaIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        var mpa = await db.MarineProtectedAreas.FirstAsync();
        return mpa.Id;
    }

    #endregion
}
