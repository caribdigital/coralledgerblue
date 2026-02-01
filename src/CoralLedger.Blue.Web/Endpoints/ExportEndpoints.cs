using System.Text;
using CoralLedger.Blue.Application.Common.Interfaces;

namespace CoralLedger.Blue.Web.Endpoints;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/export")
            .WithTags("Data Export");

        // GET /api/export/mpas/geojson - Export MPAs as GeoJSON
        group.MapGet("/mpas/geojson", async (
            IDataExportService exportService,
            string? islandGroup = null,
            string? protectionLevel = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                IslandGroup = islandGroup,
                ProtectionLevel = protectionLevel,
                Limit = limit
            };

            var geoJson = await exportService.ExportMpasAsGeoJsonAsync(options, ct).ConfigureAwait(false);

            return Results.Text(geoJson, "application/geo+json", Encoding.UTF8);
        })
        .WithName("ExportMpasGeoJson")
        .Produces<string>(contentType: "application/geo+json");

        // GET /api/export/mpas/shapefile - Export MPAs as Shapefile (zip)
        group.MapGet("/mpas/shapefile", async (
            IDataExportService exportService,
            string? islandGroup = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions { IslandGroup = islandGroup };
            var zipBytes = await exportService.ExportMpasAsShapefileAsync(options, ct).ConfigureAwait(false);

            return Results.File(zipBytes, "application/zip", "mpas.zip");
        })
        .WithName("ExportMpasShapefile")
        .Produces<byte[]>(contentType: "application/zip");

        // GET /api/export/mpas/csv - Export MPAs as CSV
        group.MapGet("/mpas/csv", async (
            IDataExportService exportService,
            string? islandGroup = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions { IslandGroup = islandGroup, Limit = limit };
            var csv = await exportService.ExportAsCsvAsync(ExportDataType.MarineProtectedAreas, options, ct).ConfigureAwait(false);

            return Results.Text(csv, "text/csv", Encoding.UTF8);
        })
        .WithName("ExportMpasCsv")
        .Produces<string>(contentType: "text/csv");

        // GET /api/export/vessels/geojson - Export vessel events as GeoJSON
        group.MapGet("/vessels/geojson", async (
            IDataExportService exportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                FromDate = fromDate,
                ToDate = toDate,
                Limit = limit ?? 1000
            };

            var geoJson = await exportService.ExportVesselEventsAsGeoJsonAsync(options, ct).ConfigureAwait(false);
            return Results.Text(geoJson, "application/geo+json", Encoding.UTF8);
        })
        .WithName("ExportVesselsGeoJson")
        .Produces<string>(contentType: "application/geo+json");

        // GET /api/export/vessels/csv - Export vessel events as CSV
        group.MapGet("/vessels/csv", async (
            IDataExportService exportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                FromDate = fromDate,
                ToDate = toDate,
                Limit = limit ?? 1000
            };

            var csv = await exportService.ExportAsCsvAsync(ExportDataType.VesselEvents, options, ct).ConfigureAwait(false);
            return Results.Text(csv, "text/csv", Encoding.UTF8);
        })
        .WithName("ExportVesselsCsv")
        .Produces<string>(contentType: "text/csv");

        // GET /api/export/bleaching/geojson - Export bleaching alerts as GeoJSON
        group.MapGet("/bleaching/geojson", async (
            IDataExportService exportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
                ToDate = toDate,
                Limit = limit ?? 500
            };

            var geoJson = await exportService.ExportBleachingAlertsAsGeoJsonAsync(options, ct).ConfigureAwait(false);
            return Results.Text(geoJson, "application/geo+json", Encoding.UTF8);
        })
        .WithName("ExportBleachingGeoJson")
        .Produces<string>(contentType: "application/geo+json");

        // GET /api/export/bleaching/csv - Export bleaching alerts as CSV
        group.MapGet("/bleaching/csv", async (
            IDataExportService exportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
                ToDate = toDate,
                Limit = limit ?? 500
            };

            var csv = await exportService.ExportAsCsvAsync(ExportDataType.BleachingAlerts, options, ct).ConfigureAwait(false);
            return Results.Text(csv, "text/csv", Encoding.UTF8);
        })
        .WithName("ExportBleachingCsv")
        .Produces<string>(contentType: "text/csv");

        // GET /api/export/observations/geojson - Export citizen observations as GeoJSON
        group.MapGet("/observations/geojson", async (
            IDataExportService exportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                FromDate = fromDate,
                ToDate = toDate,
                Limit = limit ?? 500
            };

            var geoJson = await exportService.ExportObservationsAsGeoJsonAsync(options, ct).ConfigureAwait(false);
            return Results.Text(geoJson, "application/geo+json", Encoding.UTF8);
        })
        .WithName("ExportObservationsGeoJson")
        .Produces<string>(contentType: "application/geo+json");

        // GET /api/export/observations/csv - Export citizen observations as CSV
        group.MapGet("/observations/csv", async (
            IDataExportService exportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null,
            CancellationToken ct = default) =>
        {
            var options = new ExportOptions
            {
                FromDate = fromDate,
                ToDate = toDate,
                Limit = limit ?? 500
            };

            var csv = await exportService.ExportAsCsvAsync(ExportDataType.CitizenObservations, options, ct).ConfigureAwait(false);
            return Results.Text(csv, "text/csv", Encoding.UTF8);
        })
        .WithName("ExportObservationsCsv")
        .Produces<string>(contentType: "text/csv");

        // GET /api/export/reports/mpa/{mpaId} - Generate PDF report for single MPA
        group.MapGet("/reports/mpa/{mpaId:guid}", async (
            Guid mpaId,
            IReportGenerationService reportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool includeCharts = true,
            CancellationToken ct = default) =>
        {
            try
            {
                var options = new ReportOptions
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    IncludeCharts = includeCharts
                };

                var pdfBytes = await reportService.GenerateMpaReportAsync(mpaId, options, ct).ConfigureAwait(false);
                return Results.File(pdfBytes, "application/pdf", $"mpa-report-{mpaId}.pdf");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return Results.NotFound(new { error = $"MPA with ID {mpaId} not found" });
            }
        })
        .WithName("GenerateMpaReport")
        .WithDescription("Generate a detailed PDF report for a specific Marine Protected Area")
        .Produces<byte[]>(contentType: "application/pdf")
        .ProducesProblem(404);

        // GET /api/export/reports/all-mpas - Generate PDF summary report for all MPAs
        group.MapGet("/reports/all-mpas", async (
            IReportGenerationService reportService,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? islandGroup = null,
            string? protectionLevel = null,
            bool includeCharts = true,
            CancellationToken ct = default) =>
        {
            var options = new ReportOptions
            {
                FromDate = fromDate,
                ToDate = toDate,
                IslandGroup = islandGroup,
                ProtectionLevel = protectionLevel,
                IncludeCharts = includeCharts
            };

            var pdfBytes = await reportService.GenerateAllMpasReportAsync(options, ct).ConfigureAwait(false);
            return Results.File(pdfBytes, "application/pdf", $"all-mpas-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
        })
        .WithName("GenerateAllMpasReport")
        .WithDescription("Generate a summary PDF report for all Marine Protected Areas")
        .Produces<byte[]>(contentType: "application/pdf");

        return endpoints;
    }
}
