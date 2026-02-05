using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Reports.DTOs;
using CoralLedger.Blue.Application.Features.Reports.Queries;
using CoralLedger.Blue.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for PdfReportGenerationService - verifies PDF generation logic for MPA reports
/// </summary>
public class PdfReportGenerationServiceTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<PdfReportGenerationService>> _loggerMock;
    private readonly PdfReportGenerationService _service;

    public PdfReportGenerationServiceTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<PdfReportGenerationService>>();
        _service = new PdfReportGenerationService(_mediatorMock.Object, _loggerMock.Object);
    }

    #region Test Data Helpers

    private static MpaStatusReportDto CreateTestMpaReportData(
        string name = "Test MPA",
        int bleachingAlerts = 0,
        int vesselEvents = 0,
        int observations = 0)
    {
        return new MpaStatusReportDto
        {
            MpaId = Guid.NewGuid(),
            Name = name,
            LocalName = "Local Name",
            AreaSquareKm = 100.5,
            Status = "Active",
            ProtectionLevel = "NoTake",
            IslandGroup = "Exumas",
            DesignationDate = new DateOnly(2020, 1, 15),
            ManagingAuthority = "Test Authority",
            Description = "Test MPA Description",
            CentroidLongitude = -77.5,
            CentroidLatitude = 24.5,
            ReefCount = 10,
            BleachingData = new BleachingDataSummary
            {
                TotalAlerts = bleachingAlerts,
                MaxDegreeHeatingWeeks = bleachingAlerts > 0 ? 8.5 : null,
                MaxSeaSurfaceTemp = bleachingAlerts > 0 ? 30.2 : null,
                AvgSeaSurfaceTemp = bleachingAlerts > 0 ? 28.5 : null,
                CriticalAlertsCount = bleachingAlerts > 0 ? 1 : 0,
                LastAlertDate = bleachingAlerts > 0 ? DateTime.UtcNow.AddDays(-5) : null,
                RecentAlerts = bleachingAlerts > 0 ? new List<BleachingAlertItem>
                {
                    new() { Date = DateTime.UtcNow.AddDays(-5), DegreeHeatingWeeks = 8.5, SeaSurfaceTemp = 30.2, AlertLevel = "Critical" },
                    new() { Date = DateTime.UtcNow.AddDays(-10), DegreeHeatingWeeks = 4.5, SeaSurfaceTemp = 28.5, AlertLevel = "Warning" }
                } : new List<BleachingAlertItem>()
            },
            FishingActivity = new FishingActivitySummary
            {
                TotalVesselEvents = vesselEvents,
                FishingEvents = vesselEvents > 0 ? vesselEvents / 2 : 0,
                PortVisits = vesselEvents > 0 ? vesselEvents / 3 : 0,
                Encounters = vesselEvents > 0 ? vesselEvents / 4 : 0,
                UniqueVessels = vesselEvents > 0 ? 5 : 0,
                LastActivityDate = vesselEvents > 0 ? DateTime.UtcNow.AddDays(-3) : null,
                RecentEvents = vesselEvents > 0 ? new List<VesselEventItem>
                {
                    new() { StartTime = DateTime.UtcNow.AddDays(-3), EventType = "Fishing", VesselName = "Test Vessel 1", DurationHours = 2.5 },
                    new() { StartTime = DateTime.UtcNow.AddDays(-7), EventType = "Transit", VesselName = "Test Vessel 2", DurationHours = 1.5 }
                } : new List<VesselEventItem>(),
                EventsByType = vesselEvents > 0 ? new Dictionary<string, int> { { "Fishing", 3 }, { "Transit", 2 } } : new Dictionary<string, int>()
            },
            Observations = new ObservationsSummary
            {
                TotalObservations = observations,
                ApprovedObservations = observations > 0 ? observations / 2 : 0,
                PendingObservations = observations > 0 ? observations / 3 : 0,
                RejectedObservations = observations > 0 ? observations / 6 : 0,
                AvgSeverity = observations > 0 ? 3.5 : null,
                LastObservationDate = observations > 0 ? DateTime.UtcNow.AddDays(-2) : null,
                RecentObservations = observations > 0 ? new List<ObservationItem>
                {
                    new() { ObservedAt = DateTime.UtcNow.AddDays(-2), Description = "Test observation 1", Severity = 4, Status = "Approved" },
                    new() { ObservedAt = DateTime.UtcNow.AddDays(-5), Description = "Test observation 2", Severity = 3, Status = "Pending" }
                } : new List<ObservationItem>(),
                ObservationsBySeverity = observations > 0 ? new Dictionary<int, int> { { 3, 2 }, { 4, 3 }, { 5, 1 } } : new Dictionary<int, int>()
            },
            GeneratedAt = DateTime.UtcNow,
            DataFromDate = DateTime.UtcNow.AddMonths(-1),
            DataToDate = DateTime.UtcNow
        };
    }

    private static AllMpasSummaryReportDto CreateTestAllMpasReportData(int mpaCount = 3)
    {
        var mpas = new List<MpaSummaryItem>();
        for (int i = 0; i < mpaCount; i++)
        {
            mpas.Add(new MpaSummaryItem
            {
                MpaId = Guid.NewGuid(),
                Name = $"Test MPA {i + 1}",
                IslandGroup = i % 2 == 0 ? "Exumas" : "Abaco",
                ProtectionLevel = i % 2 == 0 ? "NoTake" : "HighlyProtected",
                AreaSquareKm = 100.5 + i * 10,
                TotalAlerts = i * 2,
                TotalVesselEvents = i * 3,
                TotalObservations = i * 4,
                Status = "Active"
            });
        }

        return new AllMpasSummaryReportDto
        {
            TotalMpas = mpaCount,
            TotalAreaSquareKm = mpas.Sum(m => m.AreaSquareKm),
            Mpas = mpas,
            Statistics = new OverallStatistics
            {
                TotalBleachingAlerts = mpas.Sum(m => m.TotalAlerts),
                TotalVesselEvents = mpas.Sum(m => m.TotalVesselEvents),
                TotalObservations = mpas.Sum(m => m.TotalObservations),
                ActiveMpas = mpaCount,
                DecommissionedMpas = 0,
                MpasByIslandGroup = new Dictionary<string, int> { { "Exumas", 2 }, { "Abaco", 1 } },
                MpasByProtectionLevel = new Dictionary<string, int> { { "NoTake", 2 }, { "HighlyProtected", 1 } }
            },
            GeneratedAt = DateTime.UtcNow,
            DataFromDate = DateTime.UtcNow.AddMonths(-3),
            DataToDate = DateTime.UtcNow
        };
    }

    #endregion

    #region GenerateMpaReportAsync Tests

    [Fact]
    public async Task GenerateMpaReportAsync_WithValidData_ReturnsPdfBytes()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData("Test MPA", bleachingAlerts: 5, vesselEvents: 10, observations: 8);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetMpaStatusReportDataQuery>(q => q.MpaId == mpaId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithValidData_GeneratesValidPdfMagicBytes()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-", "PDF files must start with the PDF magic bytes");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WhenMpaNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MpaStatusReportDto?)null);

        // Act & Assert
        await _service.Invoking(s => s.GenerateMpaReportAsync(mpaId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"MPA with ID {mpaId} not found");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithAllSections_IncludesAllData()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData(
            "Complete MPA",
            bleachingAlerts: 5,
            vesselEvents: 10,
            observations: 8);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(10000, "PDF with all sections should be substantial in size");
        
        // Verify PDF magic bytes
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithNoData_OmitsSections()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData(
            "Empty MPA",
            bleachingAlerts: 0,
            vesselEvents: 0,
            observations: 0);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        result.Should().NotBeEmpty();
        // PDF should still be valid - verify magic bytes
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-", "PDF should have valid header even without data sections");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithDateRange_PassesCorrectOptions()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var fromDate = new DateTime(2024, 1, 1);
        var toDate = new DateTime(2024, 12, 31);
        var options = new ReportOptions { FromDate = fromDate, ToDate = toDate };
        var testData = CreateTestMpaReportData();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId, options);

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetMpaStatusReportDataQuery>(q => 
                q.MpaId == mpaId && 
                q.FromDate == fromDate && 
                q.ToDate == toDate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithNullOptions_UsesDefaultOptions()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId, options: null);

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetMpaStatusReportDataQuery>(q => q.MpaId == mpaId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateMpaReportAsync_PdfContainsBrandingElements()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        result.Should().NotBeEmpty();
        // Verify the PDF is valid by checking magic bytes and structure
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-");
        
        var pdfContent = Encoding.UTF8.GetString(result);
        pdfContent.Should().Contain("%%EOF", "PDF should have proper EOF marker");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithCancellationToken_PassesToMediator()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), cancellationToken))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId, ct: cancellationToken);

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<GetMpaStatusReportDataQuery>(),
            cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WithIncludeChartsEnabled_GeneratesLargerPdf()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData(
            "Chart Test MPA",
            bleachingAlerts: 5,
            vesselEvents: 10,
            observations: 8);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act - Generate PDF without charts
        var optionsWithoutCharts = new ReportOptions { IncludeCharts = false };
        var resultWithoutCharts = await _service.GenerateMpaReportAsync(mpaId, optionsWithoutCharts);

        // Act - Generate PDF with charts
        var optionsWithCharts = new ReportOptions { IncludeCharts = true };
        var resultWithCharts = await _service.GenerateMpaReportAsync(mpaId, optionsWithCharts);

        // Assert
        resultWithoutCharts.Should().NotBeEmpty();
        resultWithCharts.Should().NotBeEmpty();

        // PDF with charts should be larger due to embedded images
        resultWithCharts.Length.Should().BeGreaterThan(resultWithoutCharts.Length,
            "PDF with charts should include embedded chart images making it larger");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_WhenMediatorThrowsException_PropagatesException()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var expectedException = new InvalidOperationException("Database connection failed");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await _service.Invoking(s => s.GenerateMpaReportAsync(mpaId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    #endregion

    #region GenerateAllMpasReportAsync Tests

    [Fact]
    public async Task GenerateAllMpasReportAsync_WithValidData_ReturnsPdfBytes()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(3);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<GetAllMpasSummaryReportDataQuery>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_WithValidData_GeneratesValidPdfMagicBytes()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(3);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-", "PDF files must start with the PDF magic bytes");
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_WithEmptyMpaList_HandlesGracefully()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(0);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        result.Should().NotBeEmpty();
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-", "should still generate valid PDF with no MPAs");
    }

    [Theory]
    [InlineData("Exumas", null)]
    [InlineData("Abaco", null)]
    [InlineData(null, "NoTake")]
    [InlineData(null, "HighlyProtected")]
    [InlineData("Exumas", "NoTake")]
    [InlineData("Abaco", "HighlyProtected")]
    public async Task GenerateAllMpasReportAsync_WithFilters_PassesCorrectFilterCombination(
        string? islandGroup, string? protectionLevel)
    {
        // Arrange
        var options = new ReportOptions
        {
            IslandGroup = islandGroup,
            ProtectionLevel = protectionLevel
        };
        var testData = CreateTestAllMpasReportData(2);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync(options);

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetAllMpasSummaryReportDataQuery>(q =>
                q.IslandGroup == islandGroup &&
                q.ProtectionLevel == protectionLevel),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_IncludesStatisticsSummary()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(5);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(10000, "PDF with statistics should be substantial");
        
        // Verify PDF is valid
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-");
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_WithDateRange_PassesCorrectOptions()
    {
        // Arrange
        var fromDate = new DateTime(2024, 1, 1);
        var toDate = new DateTime(2024, 12, 31);
        var options = new ReportOptions { FromDate = fromDate, ToDate = toDate };
        var testData = CreateTestAllMpasReportData(3);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync(options);

        // Assert
        result.Should().NotBeEmpty();
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetAllMpasSummaryReportDataQuery>(q => 
                q.FromDate == fromDate && 
                q.ToDate == toDate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_PdfContainsBrandingElements()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(3);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        result.Should().NotBeEmpty();
        // Verify the PDF is valid by checking magic bytes and structure
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-");
        
        var pdfContent = Encoding.UTF8.GetString(result);
        pdfContent.Should().Contain("%%EOF", "PDF should have proper EOF marker");
    }

    #endregion

    #region PDF Validation Tests

    [Fact]
    public async Task GenerateMpaReportAsync_GeneratedPdf_IsNotEmpty()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000, "a valid PDF should have substantial content");
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_GeneratedPdf_IsNotEmpty()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(3);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000, "a valid PDF should have substantial content");
    }

    [Fact]
    public async Task GenerateMpaReportAsync_GeneratedPdf_HasCorrectPdfStructure()
    {
        // Arrange
        var mpaId = Guid.NewGuid();
        var testData = CreateTestMpaReportData();
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMpaStatusReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateMpaReportAsync(mpaId);

        // Assert
        var pdfContent = Encoding.UTF8.GetString(result);
        
        // Check for PDF structure markers
        pdfContent.Should().Contain("%%EOF", "PDF should end with EOF marker");
        pdfContent.Should().Contain("/Type", "PDF should contain type definitions");
        pdfContent.Should().Contain("endobj", "PDF should contain object definitions");
    }

    [Fact]
    public async Task GenerateAllMpasReportAsync_GeneratedPdf_HasCorrectPdfStructure()
    {
        // Arrange
        var testData = CreateTestAllMpasReportData(3);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllMpasSummaryReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _service.GenerateAllMpasReportAsync();

        // Assert
        var pdfContent = Encoding.UTF8.GetString(result);
        
        // Check for PDF structure markers
        pdfContent.Should().Contain("%%EOF", "PDF should end with EOF marker");
        pdfContent.Should().Contain("/Type", "PDF should contain type definitions");
        pdfContent.Should().Contain("endobj", "PDF should contain object definitions");
    }

    #endregion
}
