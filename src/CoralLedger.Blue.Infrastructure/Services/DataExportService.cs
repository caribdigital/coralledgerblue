using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace CoralLedger.Blue.Infrastructure.Services;

public class DataExportService : IDataExportService
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<DataExportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DataExportService(IMarineDbContext context, ILogger<DataExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> ExportMpasAsGeoJsonAsync(ExportOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ExportOptions();

        var query = _context.MarineProtectedAreas.AsQueryable();

        if (!string.IsNullOrEmpty(options.IslandGroup) && Enum.TryParse<IslandGroup>(options.IslandGroup, true, out var islandGroup))
            query = query.Where(m => m.IslandGroup == islandGroup);

        if (!string.IsNullOrEmpty(options.ProtectionLevel) && Enum.TryParse<ProtectionLevel>(options.ProtectionLevel, true, out var level))
            query = query.Where(m => m.ProtectionLevel == level);

        if (options.MpaIds?.Count > 0)
            query = query.Where(m => options.MpaIds.Contains(m.Id));

        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);

        var mpas = await query.ToListAsync(ct).ConfigureAwait(false);

        var features = mpas.Select(mpa => new Feature(
            mpa.Boundary ?? mpa.Centroid,
            new AttributesTable
            {
                { "id", mpa.Id.ToString() },
                { "name", mpa.Name },
                { "islandGroup", mpa.IslandGroup.ToString() },
                { "protectionLevel", mpa.ProtectionLevel.ToString() },
                { "areaKm2", mpa.AreaSquareKm },
                { "designationDate", mpa.DesignationDate?.ToString("yyyy-MM-dd") ?? "" },
                { "managingAuthority", mpa.ManagingAuthority ?? "" },
                { "status", mpa.Status.ToString() }
            }
        )).ToList();

        return SerializeToGeoJson(features);
    }

    public async Task<string> ExportVesselEventsAsGeoJsonAsync(ExportOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ExportOptions();

        var query = _context.VesselEvents
            .Include(e => e.Vessel)
            .Include(e => e.MarineProtectedArea)
            .AsQueryable();

        if (options.FromDate.HasValue)
            query = query.Where(e => e.StartTime >= options.FromDate.Value);

        if (options.ToDate.HasValue)
            query = query.Where(e => e.StartTime <= options.ToDate.Value);

        if (options.MpaIds?.Count > 0)
            query = query.Where(e => e.MarineProtectedAreaId.HasValue && options.MpaIds.Contains(e.MarineProtectedAreaId.Value));

        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);

        var events = await query.ToListAsync(ct).ConfigureAwait(false);

        var features = events.Select(e => new Feature(
            e.Location,
            new AttributesTable
            {
                { "id", e.Id.ToString() },
                { "eventType", e.EventType.ToString() },
                { "vesselName", e.Vessel?.Name ?? "" },
                { "vesselMmsi", e.Vessel?.Mmsi ?? "" },
                { "startTime", e.StartTime.ToString("o") },
                { "endTime", e.EndTime?.ToString("o") ?? "" },
                { "durationHours", e.DurationHours ?? 0 },
                { "mpaName", e.MarineProtectedArea?.Name ?? "" },
                { "isInMpa", e.IsInMpa ?? false }
            }
        )).ToList();

        return SerializeToGeoJson(features);
    }

    public async Task<string> ExportBleachingAlertsAsGeoJsonAsync(ExportOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ExportOptions();

        var query = _context.BleachingAlerts
            .Include(b => b.MarineProtectedArea)
            .AsQueryable();

        if (options.FromDate.HasValue)
        {
            var fromDate = DateOnly.FromDateTime(options.FromDate.Value);
            query = query.Where(b => b.Date >= fromDate);
        }

        if (options.ToDate.HasValue)
        {
            var toDate = DateOnly.FromDateTime(options.ToDate.Value);
            query = query.Where(b => b.Date <= toDate);
        }

        if (options.MpaIds?.Count > 0)
            query = query.Where(b => b.MarineProtectedAreaId.HasValue && options.MpaIds.Contains(b.MarineProtectedAreaId.Value));

        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);

        var alerts = await query.ToListAsync(ct).ConfigureAwait(false);

        var features = alerts.Select(b => new Feature(
            b.Location,
            new AttributesTable
            {
                { "id", b.Id.ToString() },
                { "date", b.Date.ToString("yyyy-MM-dd") },
                { "alertLevel", b.AlertLevel.ToString() },
                { "degreeHeatingWeek", b.DegreeHeatingWeek },
                { "seaSurfaceTemperature", b.SeaSurfaceTemperature },
                { "sstAnomaly", b.SstAnomaly },
                { "mpaName", b.MarineProtectedArea?.Name ?? "" }
            }
        )).ToList();

        return SerializeToGeoJson(features);
    }

    public async Task<string> ExportObservationsAsGeoJsonAsync(ExportOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ExportOptions();

        var query = _context.CitizenObservations
            .Include(o => o.MarineProtectedArea)
            .Where(o => o.Status == ObservationStatus.Approved)
            .AsQueryable();

        if (options.FromDate.HasValue)
            query = query.Where(o => o.ObservationTime >= options.FromDate.Value);

        if (options.ToDate.HasValue)
            query = query.Where(o => o.ObservationTime <= options.ToDate.Value);

        if (options.MpaIds?.Count > 0)
            query = query.Where(o => o.MarineProtectedAreaId.HasValue && options.MpaIds.Contains(o.MarineProtectedAreaId.Value));

        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);

        var observations = await query.ToListAsync(ct).ConfigureAwait(false);

        var features = observations.Select(o => new Feature(
            o.Location,
            new AttributesTable
            {
                { "id", o.Id.ToString() },
                { "title", o.Title },
                { "type", o.Type.ToString() },
                { "severity", o.Severity },
                { "observationTime", o.ObservationTime.ToString("o") },
                { "description", o.Description ?? "" },
                { "mpaName", o.MarineProtectedArea?.Name ?? "" },
                { "citizenName", o.CitizenName ?? "Anonymous" }
            }
        )).ToList();

        return SerializeToGeoJson(features);
    }

    public async Task<byte[]> ExportMpasAsShapefileAsync(ExportOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ExportOptions();

        var query = _context.MarineProtectedAreas.AsQueryable();

        if (!string.IsNullOrEmpty(options.IslandGroup) && Enum.TryParse<IslandGroup>(options.IslandGroup, true, out var islandGroup))
            query = query.Where(m => m.IslandGroup == islandGroup);

        if (options.MpaIds?.Count > 0)
            query = query.Where(m => options.MpaIds.Contains(m.Id));

        var mpas = await query.ToListAsync(ct).ConfigureAwait(false);

        // Create shapefile as zip with GeoJSON inside (simplified approach without NetTopologySuite.IO.ShapeFile)
        // For production, consider adding NetTopologySuite.IO.ShapeFile package
        using var memoryStream = new MemoryStream();
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true);

        // Create GeoJSON file within the zip
        var features = mpas
            .Where(m => m.Boundary != null)
            .Select(mpa => new Feature(
                mpa.Boundary!,
                new AttributesTable
                {
                    { "id", mpa.Id.ToString() },
                    { "name", TruncateString(mpa.Name, 50) },
                    { "island", mpa.IslandGroup.ToString() },
                    { "protection", mpa.ProtectionLevel.ToString() },
                    { "area_km2", mpa.AreaSquareKm }
                }
            )).ToList();

        // Write GeoJSON to zip
        var geoJsonEntry = archive.CreateEntry("mpas.geojson");
        using (var entryStream = geoJsonEntry.Open())
        using (var writer = new StreamWriter(entryStream))
        {
            var geoJson = SerializeToGeoJson(features.ToList());
            await writer.WriteAsync(geoJson).ConfigureAwait(false);
        }

        // Add PRJ file with WGS84 projection info
        var prjEntry = archive.CreateEntry("mpas.prj");
        using (var prjStream = prjEntry.Open())
        using (var prjWriter = new StreamWriter(prjStream))
        {
            await prjWriter.WriteAsync("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]").ConfigureAwait(false);
        }

        // Add README for conversion instructions
        var readmeEntry = archive.CreateEntry("README.txt");
        using (var readmeStream = readmeEntry.Open())
        using (var readmeWriter = new StreamWriter(readmeStream))
        {
            await readmeWriter.WriteAsync("This archive contains MPA data in GeoJSON format.\nTo convert to Shapefile, use QGIS or ogr2ogr:\n  ogr2ogr -f \"ESRI Shapefile\" mpas.shp mpas.geojson").ConfigureAwait(false);
        }

        archive.Dispose();
        return memoryStream.ToArray();
    }

    public async Task<string> ExportAsCsvAsync(ExportDataType dataType, ExportOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        switch (dataType)
        {
            case ExportDataType.MarineProtectedAreas:
                sb.AppendLine("Id,Name,IslandGroup,ProtectionLevel,AreaKm2,DesignationDate,ManagingAuthority,Longitude,Latitude");
                var mpas = await GetFilteredMpas(options, ct).ConfigureAwait(false);
                foreach (var mpa in mpas)
                {
                    var centroid = mpa.Centroid ?? mpa.Boundary?.Centroid;
                    sb.AppendLine($"\"{mpa.Id}\",\"{EscapeCsv(mpa.Name)}\",\"{mpa.IslandGroup}\",\"{mpa.ProtectionLevel}\",{mpa.AreaSquareKm},{mpa.DesignationDate?.ToString("yyyy-MM-dd")},\"{EscapeCsv(mpa.ManagingAuthority)}\",{centroid?.X},{centroid?.Y}");
                }
                break;

            case ExportDataType.VesselEvents:
                sb.AppendLine("Id,EventType,VesselName,VesselMmsi,StartTime,EndTime,DurationHours,Longitude,Latitude,MpaName,IsInMpa");
                var events = await GetFilteredVesselEvents(options, ct).ConfigureAwait(false);
                foreach (var e in events)
                {
                    sb.AppendLine($"\"{e.Id}\",\"{e.EventType}\",\"{EscapeCsv(e.Vessel?.Name)}\",\"{e.Vessel?.Mmsi}\",{e.StartTime:o},{e.EndTime?.ToString("o")},{e.DurationHours},{e.Location?.X},{e.Location?.Y},\"{EscapeCsv(e.MarineProtectedArea?.Name)}\",{e.IsInMpa}");
                }
                break;

            case ExportDataType.BleachingAlerts:
                sb.AppendLine("Id,Date,AlertLevel,DegreeHeatingWeek,SeaSurfaceTemperature,SstAnomaly,Longitude,Latitude,MpaName");
                var alerts = await GetFilteredBleachingAlerts(options, ct).ConfigureAwait(false);
                foreach (var b in alerts)
                {
                    sb.AppendLine($"\"{b.Id}\",{b.Date:yyyy-MM-dd},\"{b.AlertLevel}\",{b.DegreeHeatingWeek},{b.SeaSurfaceTemperature},{b.SstAnomaly},{b.Location?.X},{b.Location?.Y},\"{EscapeCsv(b.MarineProtectedArea?.Name)}\"");
                }
                break;

            case ExportDataType.CitizenObservations:
                sb.AppendLine("Id,Title,Type,Severity,ObservationTime,Description,Longitude,Latitude,MpaName,CitizenName");
                var observations = await GetFilteredObservations(options, ct).ConfigureAwait(false);
                foreach (var o in observations)
                {
                    sb.AppendLine($"\"{o.Id}\",\"{EscapeCsv(o.Title)}\",\"{o.Type}\",{o.Severity},{o.ObservationTime:o},\"{EscapeCsv(o.Description)}\",{o.Location?.X},{o.Location?.Y},\"{EscapeCsv(o.MarineProtectedArea?.Name)}\",\"{EscapeCsv(o.CitizenName)}\"");
                }
                break;
        }

        return sb.ToString();
    }

    private string SerializeToGeoJson(List<Feature> features)
    {
        var collection = new FeatureCollection();
        foreach (var feature in features)
        {
            collection.Add(feature);
        }

        var writer = new GeoJsonWriter();
        return writer.Write(collection);
    }

    private async Task<List<Domain.Entities.MarineProtectedArea>> GetFilteredMpas(ExportOptions options, CancellationToken ct)
    {
        var query = _context.MarineProtectedAreas.AsQueryable();
        if (!string.IsNullOrEmpty(options.IslandGroup) && Enum.TryParse<IslandGroup>(options.IslandGroup, true, out var islandGroup))
            query = query.Where(m => m.IslandGroup == islandGroup);
        if (options.MpaIds?.Count > 0)
            query = query.Where(m => options.MpaIds.Contains(m.Id));
        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);
        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    private async Task<List<Domain.Entities.VesselEvent>> GetFilteredVesselEvents(ExportOptions options, CancellationToken ct)
    {
        var query = _context.VesselEvents.Include(e => e.Vessel).Include(e => e.MarineProtectedArea).AsQueryable();
        if (options.FromDate.HasValue)
            query = query.Where(e => e.StartTime >= options.FromDate.Value);
        if (options.ToDate.HasValue)
            query = query.Where(e => e.StartTime <= options.ToDate.Value);
        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);
        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    private async Task<List<Domain.Entities.BleachingAlert>> GetFilteredBleachingAlerts(ExportOptions options, CancellationToken ct)
    {
        var query = _context.BleachingAlerts.Include(b => b.MarineProtectedArea).AsQueryable();
        if (options.FromDate.HasValue)
        {
            var fromDate = DateOnly.FromDateTime(options.FromDate.Value);
            query = query.Where(b => b.Date >= fromDate);
        }
        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);
        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    private async Task<List<Domain.Entities.CitizenObservation>> GetFilteredObservations(ExportOptions options, CancellationToken ct)
    {
        var query = _context.CitizenObservations.Include(o => o.MarineProtectedArea)
            .Where(o => o.Status == ObservationStatus.Approved).AsQueryable();
        if (options.FromDate.HasValue)
            query = query.Where(o => o.ObservationTime >= options.FromDate.Value);
        if (options.Limit.HasValue)
            query = query.Take(options.Limit.Value);
        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    private static string TruncateString(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= maxLength ? value : value.Substring(0, maxLength);

    private static string EscapeCsv(string? value) =>
        string.IsNullOrEmpty(value) ? "" : value.Replace("\"", "\"\"");
}
