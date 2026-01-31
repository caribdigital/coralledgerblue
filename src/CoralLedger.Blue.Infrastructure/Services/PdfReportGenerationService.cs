using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Reports.DTOs;
using CoralLedger.Blue.Application.Features.Reports.Queries;
using MediatR;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Service for generating PDF reports using QuestPDF
/// </summary>
public class PdfReportGenerationService : IReportGenerationService
{
    private readonly IMediator _mediator;
    private readonly ILogger<PdfReportGenerationService> _logger;

    public PdfReportGenerationService(IMediator mediator, ILogger<PdfReportGenerationService> logger)
    {
        _mediator = mediator;
        _logger = logger;

        // Configure QuestPDF license (Community license for open-source projects)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateMpaReportAsync(Guid mpaId, ReportOptions? options = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating PDF report for MPA {MpaId}", mpaId);

        options ??= new ReportOptions();

        // Get report data using query handler
        var reportData = await _mediator.Send(
            new GetMpaStatusReportDataQuery(mpaId, options.FromDate, options.ToDate),
            ct).ConfigureAwait(false);

        if (reportData == null)
        {
            throw new InvalidOperationException($"MPA with ID {mpaId} not found");
        }

        // Generate PDF
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, PdfUnit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, reportData));
                page.Content().Element(c => ComposeContent(c, reportData, options));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateAllMpasReportAsync(ReportOptions? options = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating PDF summary report for all MPAs");

        options ??= new ReportOptions();

        // Get report data using query handler
        var reportData = await _mediator.Send(
            new GetAllMpasSummaryReportDataQuery(
                options.FromDate,
                options.ToDate,
                options.IslandGroup,
                options.ProtectionLevel,
                options.MpaIds),
            ct).ConfigureAwait(false);

        // Generate PDF
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, PdfUnit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeSummaryHeader(c, reportData));
                page.Content().Element(c => ComposeSummaryContent(c, reportData));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, MpaStatusReportDto data)
    {
        container.Column(column =>
        {
            column.Spacing(5);

            // CoralLedger branding
            column.Item().Background(Colors.Blue.Darken3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("CoralLedger Blue").FontSize(20).Bold().FontColor(Colors.White);
                    col.Item().Text("Marine Protected Area Status Report").FontSize(12).FontColor(Colors.White);
                });
                row.ConstantItem(80).AlignRight().Text($"{DateTime.UtcNow:yyyy-MM-dd}").FontColor(Colors.White);
            });

            // MPA title
            column.Item().PaddingVertical(10).Column(col =>
            {
                col.Item().Text(data.Name).FontSize(18).Bold();
                if (!string.IsNullOrEmpty(data.LocalName))
                {
                    col.Item().Text(data.LocalName).FontSize(12).Italic().FontColor(Colors.Grey.Darken1);
                }
            });

            // Horizontal line
            column.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken1);
        });
    }

    private void ComposeContent(IContainer container, MpaStatusReportDto data, ReportOptions options)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(15);

            // MPA Overview Section
            column.Item().Element(c => ComposeMpaOverview(c, data));

            // Date range info
            column.Item()
                .Text($"Report Period: {data.DataFromDate:yyyy-MM-dd} to {data.DataToDate:yyyy-MM-dd}")
                .FontSize(9)
                .FontColor(Colors.Grey.Darken1);

            // Bleaching Data Section
            if (data.BleachingData.TotalAlerts > 0)
            {
                column.Item().Element(c => ComposeBleachingSection(c, data.BleachingData));
            }

            // Fishing Activity Section
            if (data.FishingActivity.TotalVesselEvents > 0)
            {
                column.Item().Element(c => ComposeFishingActivitySection(c, data.FishingActivity));
            }

            // Observations Section
            if (data.Observations.TotalObservations > 0)
            {
                column.Item().Element(c => ComposeObservationsSection(c, data.Observations));
            }

            // Summary footer
            column.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Lighten1)
                .Text($"Report generated on {data.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private void ComposeMpaOverview(IContainer container, MpaStatusReportDto data)
    {
        container.Column(column =>
        {
            column.Item().Text("MPA Overview").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            column.Item().PaddingVertical(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                });

                table.Cell().Element(CellStyle).Text("Island Group:").Bold();
                table.Cell().Element(CellStyle).Text(data.IslandGroup);

                table.Cell().Element(CellStyle).Text("Protection Level:").Bold();
                table.Cell().Element(CellStyle).Text(data.ProtectionLevel);

                table.Cell().Element(CellStyle).Text("Status:").Bold();
                table.Cell().Element(CellStyle).Text(data.Status);

                table.Cell().Element(CellStyle).Text("Area:").Bold();
                table.Cell().Element(CellStyle).Text($"{data.AreaSquareKm:F2} km²");

                if (data.DesignationDate.HasValue)
                {
                    table.Cell().Element(CellStyle).Text("Designation Date:").Bold();
                    table.Cell().Element(CellStyle).Text(data.DesignationDate.Value.ToString("yyyy-MM-dd"));
                }

                if (!string.IsNullOrEmpty(data.ManagingAuthority))
                {
                    table.Cell().Element(CellStyle).Text("Managing Authority:").Bold();
                    table.Cell().Element(CellStyle).Text(data.ManagingAuthority);
                }

                table.Cell().Element(CellStyle).Text("Reef Count:").Bold();
                table.Cell().Element(CellStyle).Text(data.ReefCount.ToString());

                table.Cell().Element(CellStyle).Text("Location:").Bold();
                table.Cell().Element(CellStyle).Text($"{data.CentroidLatitude:F4}°, {data.CentroidLongitude:F4}°");
            });

            if (!string.IsNullOrEmpty(data.Description))
            {
                column.Item().PaddingTop(5).Text(data.Description).FontSize(9);
            }
        });

        static IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
    }

    private void ComposeBleachingSection(IContainer container, BleachingDataSummary bleaching)
    {
        container.Column(column =>
        {
            column.Item().Text("Coral Bleaching Status").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            
            column.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total Alerts: {bleaching.TotalAlerts}").Bold();
                    col.Item().Text($"Critical Alerts: {bleaching.CriticalAlertsCount}")
                        .FontColor(bleaching.CriticalAlertsCount > 0 ? Colors.Red.Darken1 : Colors.Green.Darken1);
                });
                
                row.RelativeItem().Column(col =>
                {
                    if (bleaching.MaxDegreeHeatingWeeks.HasValue)
                    {
                        col.Item().Text($"Max DHW: {bleaching.MaxDegreeHeatingWeeks:F2}°C-weeks");
                    }
                    if (bleaching.MaxSeaSurfaceTemp.HasValue)
                    {
                        col.Item().Text($"Max SST: {bleaching.MaxSeaSurfaceTemp:F2}°C");
                    }
                });
            });

            if (bleaching.RecentAlerts.Any())
            {
                column.Item().PaddingTop(10).Text("Recent Alerts").FontSize(11).Bold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Cell().Element(HeaderStyle).Text("Date");
                    table.Cell().Element(HeaderStyle).Text("DHW (°C-weeks)");
                    table.Cell().Element(HeaderStyle).Text("SST (°C)");
                    table.Cell().Element(HeaderStyle).Text("Level");

                    // Data rows
                    foreach (var alert in bleaching.RecentAlerts.Take(5))
                    {
                        table.Cell().Element(CellStyle).Text(alert.Date.ToString("yyyy-MM-dd"));
                        table.Cell().Element(CellStyle).Text($"{alert.DegreeHeatingWeeks:F2}");
                        table.Cell().Element(CellStyle).Text($"{alert.SeaSurfaceTemp:F2}");
                        table.Cell().Element(CellStyle).Text(alert.AlertLevel)
                            .FontColor(alert.AlertLevel == "Critical" ? Colors.Red.Darken1 : 
                                      alert.AlertLevel == "Warning" ? Colors.Orange.Darken1 : Colors.Blue.Darken1);
                    }
                });
            }
        });

        static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Lighten3).Padding(5).BorderBottom(1).BorderColor(Colors.Blue.Darken1);
        static IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);
    }

    private void ComposeFishingActivitySection(IContainer container, FishingActivitySummary fishing)
    {
        container.Column(column =>
        {
            column.Item().Text("Fishing & Vessel Activity").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            
            column.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total Events: {fishing.TotalVesselEvents}").Bold();
                    col.Item().Text($"Unique Vessels: {fishing.UniqueVessels}");
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Fishing Events: {fishing.FishingEvents}");
                    col.Item().Text($"Port Visits: {fishing.PortVisits}");
                    col.Item().Text($"Encounters: {fishing.Encounters}");
                });
            });

            if (fishing.RecentEvents.Any())
            {
                column.Item().PaddingTop(10).Text("Recent Events").FontSize(11).Bold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Cell().Element(HeaderStyle).Text("Date");
                    table.Cell().Element(HeaderStyle).Text("Type");
                    table.Cell().Element(HeaderStyle).Text("Vessel");
                    table.Cell().Element(HeaderStyle).Text("Duration (hrs)");

                    // Data rows
                    foreach (var evt in fishing.RecentEvents.Take(5))
                    {
                        table.Cell().Element(CellStyle).Text(evt.StartTime.ToString("yyyy-MM-dd HH:mm"));
                        table.Cell().Element(CellStyle).Text(evt.EventType);
                        table.Cell().Element(CellStyle).Text(evt.VesselName);
                        table.Cell().Element(CellStyle).Text(evt.DurationHours.HasValue ? $"{evt.DurationHours:F1}" : "-");
                    }
                });
            }
        });

        static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Lighten3).Padding(5).BorderBottom(1).BorderColor(Colors.Blue.Darken1);
        static IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);
    }

    private void ComposeObservationsSection(IContainer container, ObservationsSummary observations)
    {
        container.Column(column =>
        {
            column.Item().Text("Citizen Observations").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            
            column.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total Observations: {observations.TotalObservations}").Bold();
                    col.Item().Text($"Approved: {observations.ApprovedObservations}")
                        .FontColor(Colors.Green.Darken1);
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Pending Review: {observations.PendingObservations}")
                        .FontColor(Colors.Orange.Darken1);
                    col.Item().Text($"Rejected: {observations.RejectedObservations}");
                    if (observations.AvgSeverity.HasValue)
                    {
                        col.Item().Text($"Avg Severity: {observations.AvgSeverity:F1}/5");
                    }
                });
            });

            if (observations.RecentObservations.Any())
            {
                column.Item().PaddingTop(10).Text("Recent Observations").FontSize(11).Bold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Cell().Element(HeaderStyle).Text("Date");
                    table.Cell().Element(HeaderStyle).Text("Description");
                    table.Cell().Element(HeaderStyle).Text("Severity");
                    table.Cell().Element(HeaderStyle).Text("Status");

                    // Data rows
                    foreach (var obs in observations.RecentObservations.Take(5))
                    {
                        table.Cell().Element(CellStyle).Text(obs.ObservedAt.ToString("yyyy-MM-dd"));
                        table.Cell().Element(CellStyle).Text(obs.Description.Length > 50 ? 
                            obs.Description.Substring(0, 47) + "..." : obs.Description);
                        table.Cell().Element(CellStyle).Text($"{obs.Severity}/5");
                        table.Cell().Element(CellStyle).Text(obs.Status);
                    }
                });
            }
        });

        static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Lighten3).Padding(5).BorderBottom(1).BorderColor(Colors.Blue.Darken1);
        static IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5);
    }

    private void ComposeSummaryHeader(IContainer container, AllMpasSummaryReportDto data)
    {
        container.Column(column =>
        {
            column.Spacing(5);

            // CoralLedger branding
            column.Item().Background(Colors.Blue.Darken3).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("CoralLedger Blue").FontSize(20).Bold().FontColor(Colors.White);
                    col.Item().Text("All MPAs Summary Report").FontSize(12).FontColor(Colors.White);
                });
                row.ConstantItem(80).AlignRight().Text($"{DateTime.UtcNow:yyyy-MM-dd}").FontColor(Colors.White);
            });

            // Summary stats
            column.Item().PaddingVertical(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total MPAs: {data.TotalMpas}").FontSize(16).Bold();
                    col.Item().Text($"Total Area: {data.TotalAreaSquareKm:F2} km²").FontSize(12);
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Report Period:").FontSize(12).Bold();
                    col.Item().Text($"{data.DataFromDate:yyyy-MM-dd} to {data.DataToDate:yyyy-MM-dd}").FontSize(10);
                });
            });

            column.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken1);
        });
    }

    private void ComposeSummaryContent(IContainer container, AllMpasSummaryReportDto data)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(15);

            // Overall Statistics
            column.Item().Element(c => ComposeOverallStatistics(c, data.Statistics));

            // MPAs Table
            column.Item().Element(c => ComposeMpasTable(c, data.Mpas));
        });
    }

    private void ComposeOverallStatistics(IContainer container, OverallStatistics stats)
    {
        container.Column(column =>
        {
            column.Item().Text("Overall Statistics").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            
            column.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total Bleaching Alerts: {stats.TotalBleachingAlerts}");
                    col.Item().Text($"Total Vessel Events: {stats.TotalVesselEvents}");
                    col.Item().Text($"Total Observations: {stats.TotalObservations}");
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Active MPAs: {stats.ActiveMpas}")
                        .FontColor(Colors.Green.Darken1);
                    col.Item().Text($"Decommissioned MPAs: {stats.DecommissionedMpas}");
                });
            });

            if (stats.MpasByIslandGroup.Any())
            {
                column.Item().PaddingTop(10).Text("MPAs by Island Group").FontSize(11).Bold();
                column.Item().Column(col =>
                {
                    foreach (var group in stats.MpasByIslandGroup.OrderByDescending(g => g.Value))
                    {
                        col.Item().Text($"{group.Key}: {group.Value}");
                    }
                });
            }
        });
    }

    private void ComposeMpasTable(IContainer container, List<MpaSummaryItem> mpas)
    {
        container.Column(column =>
        {
            column.Item().Text("Marine Protected Areas").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
            
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Cell().Element(HeaderStyle).Text("Name");
                table.Cell().Element(HeaderStyle).Text("Island Group");
                table.Cell().Element(HeaderStyle).Text("Protection");
                table.Cell().Element(HeaderStyle).Text("Area (km²)");
                table.Cell().Element(HeaderStyle).Text("Alerts");
                table.Cell().Element(HeaderStyle).Text("Vessels");
                table.Cell().Element(HeaderStyle).Text("Obs");

                // Data rows
                foreach (var mpa in mpas)
                {
                    table.Cell().Element(CellStyle).Text(mpa.Name);
                    table.Cell().Element(CellStyle).Text(mpa.IslandGroup);
                    table.Cell().Element(CellStyle).Text(mpa.ProtectionLevel);
                    table.Cell().Element(CellStyle).Text($"{mpa.AreaSquareKm:F1}");
                    table.Cell().Element(CellStyle).Text(mpa.TotalAlerts.ToString());
                    table.Cell().Element(CellStyle).Text(mpa.TotalVesselEvents.ToString());
                    table.Cell().Element(CellStyle).Text(mpa.TotalObservations.ToString());
                }
            });
        });

        static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Lighten3).Padding(5).BorderBottom(1).BorderColor(Colors.Blue.Darken1);
        static IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(3);
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated by ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span("CoralLedger Blue").FontSize(8).Bold().FontColor(Colors.Blue.Darken2);
            text.Span(" - Marine Conservation Management Platform").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
