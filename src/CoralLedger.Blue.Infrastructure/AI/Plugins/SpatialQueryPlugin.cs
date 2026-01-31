using System.ComponentModel;
using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace CoralLedger.Blue.Infrastructure.AI.Plugins;

/// <summary>
/// Semantic Kernel plugin for spatial queries using PostGIS
/// </summary>
public class SpatialQueryPlugin
{
    private readonly IMarineDbContext _context;

    public SpatialQueryPlugin(IMarineDbContext context)
    {
        _context = context;
    }

    [KernelFunction("find_nearest_mpa")]
    [Description("Find the nearest Marine Protected Area to given coordinates")]
    public async Task<string> FindNearestMpaAsync(
        [Description("Longitude (e.g., -77.5)")] double longitude,
        [Description("Latitude (e.g., 24.5)")] double latitude)
    {
        // Use raw SQL with PostGIS for spatial query
        var sql = @"
            SELECT
                m.""Name"",
                m.""ProtectionLevel"",
                m.""AreaSquareKm"",
                m.""IslandGroup"",
                ST_Distance(
                    m.""Centroid""::geography,
                    ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography
                ) / 1000 as distance_km
            FROM marine_protected_areas m
            WHERE m.""Centroid"" IS NOT NULL
            ORDER BY m.""Centroid"" <-> ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)
            LIMIT 5";

        try
        {
            var results = await _context.Database
                .SqlQueryRaw<NearestMpaResult>(sql,
                    new Npgsql.NpgsqlParameter("@lon", longitude),
                    new Npgsql.NpgsqlParameter("@lat", latitude))
                .ToListAsync().ConfigureAwait(false);

            if (results.Count == 0)
                return "No MPAs found in the database";

            var result = $"5 nearest MPAs to ({latitude:F4}, {longitude:F4}):\n";
            foreach (var mpa in results)
            {
                result += $"- {mpa.Name}: {mpa.distance_km:F1} km away ({mpa.ProtectionLevel}, {mpa.IslandGroup})\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error executing spatial query: {ex.Message}";
        }
    }

    [KernelFunction("find_mpas_in_radius")]
    [Description("Find all Marine Protected Areas within a given radius of coordinates")]
    public async Task<string> FindMpasInRadiusAsync(
        [Description("Longitude")] double longitude,
        [Description("Latitude")] double latitude,
        [Description("Radius in kilometers")] double radiusKm)
    {
        var sql = @"
            SELECT
                m.""Name"",
                m.""ProtectionLevel"",
                m.""AreaSquareKm"",
                ST_Distance(
                    m.""Centroid""::geography,
                    ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography
                ) / 1000 as distance_km
            FROM marine_protected_areas m
            WHERE m.""Centroid"" IS NOT NULL
              AND ST_DWithin(
                    m.""Centroid""::geography,
                    ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography,
                    @radius_m
                  )
            ORDER BY distance_km";

        try
        {
            var results = await _context.Database
                .SqlQueryRaw<NearestMpaResult>(sql,
                    new Npgsql.NpgsqlParameter("@lon", longitude),
                    new Npgsql.NpgsqlParameter("@lat", latitude),
                    new Npgsql.NpgsqlParameter("@radius_m", radiusKm * 1000))
                .ToListAsync().ConfigureAwait(false);

            if (results.Count == 0)
                return $"No MPAs found within {radiusKm} km of ({latitude:F4}, {longitude:F4})";

            var result = $"Found {results.Count} MPAs within {radiusKm} km:\n";
            foreach (var mpa in results)
            {
                result += $"- {mpa.Name}: {mpa.distance_km:F1} km ({mpa.ProtectionLevel})\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error executing spatial query: {ex.Message}";
        }
    }

    [KernelFunction("get_mpa_containing_point")]
    [Description("Check if a location is inside any Marine Protected Area")]
    public async Task<string> GetMpaContainingPointAsync(
        [Description("Longitude")] double longitude,
        [Description("Latitude")] double latitude)
    {
        var sql = @"
            SELECT
                m.""Name"",
                m.""ProtectionLevel"",
                m.""AreaSquareKm"",
                m.""IslandGroup""
            FROM marine_protected_areas m
            WHERE ST_Contains(m.""Boundary"", ST_SetSRID(ST_MakePoint(@lon, @lat), 4326))
            LIMIT 1";

        try
        {
            var results = await _context.Database
                .SqlQueryRaw<MpaContainsResult>(sql,
                    new Npgsql.NpgsqlParameter("@lon", longitude),
                    new Npgsql.NpgsqlParameter("@lat", latitude))
                .ToListAsync().ConfigureAwait(false);

            if (results.Count == 0)
                return $"Location ({latitude:F4}, {longitude:F4}) is NOT inside any Marine Protected Area";

            var mpa = results[0];
            return $"Location ({latitude:F4}, {longitude:F4}) is INSIDE:\n" +
                   $"- {mpa.Name}\n" +
                   $"- Protection Level: {mpa.ProtectionLevel}\n" +
                   $"- Area: {mpa.AreaSquareKm:N0} km²\n" +
                   $"- Island Group: {mpa.IslandGroup}";
        }
        catch (Exception ex)
        {
            return $"Error executing spatial query: {ex.Message}";
        }
    }

    [KernelFunction("get_fishing_events_in_mpa")]
    [Description("Get fishing events that occurred inside a specific MPA")]
    public async Task<string> GetFishingEventsInMpaAsync(
        [Description("Name of the Marine Protected Area")] string mpaName,
        [Description("Number of days to look back (default 30)")] int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var mpa = await _context.MarineProtectedAreas
            .Where(m => EF.Functions.ILike(m.Name, $"%{mpaName}%"))
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (mpa == null)
            return $"No MPA found matching '{mpaName}'";

        var events = await _context.VesselEvents
            .Include(e => e.Vessel)
            .Where(e => e.MarineProtectedAreaId == mpa.Id)
            .Where(e => e.StartTime >= since)
            .OrderByDescending(e => e.StartTime)
            .Take(20)
            .Select(e => new
            {
                VesselName = e.Vessel.Name,
                e.StartTime,
                e.DurationHours,
                e.EventType
            })
            .ToListAsync().ConfigureAwait(false);

        if (events.Count == 0)
            return $"No fishing events recorded in {mpa.Name} in the past {days} days";

        var result = $"Fishing events in {mpa.Name} (past {days} days):\n";
        foreach (var evt in events)
        {
            result += $"- {evt.VesselName}: {evt.StartTime:MMM d, yyyy} ({evt.DurationHours:F1}h)\n";
        }

        return result;
    }

    [KernelFunction("get_bleaching_hotspots")]
    [Description("Find areas with highest bleaching risk based on recent alerts")]
    public async Task<string> GetBleachingHotspotsAsync()
    {
        var sql = @"
            SELECT
                m.""Name"" as MpaName,
                MAX(b.""DegreeHeatingWeek"") as MaxDHW,
                MAX(b.""AlertLevel""::text) as HighestAlert,
                COUNT(*) as AlertCount
            FROM bleaching_alerts b
            JOIN marine_protected_areas m ON b.""MarineProtectedAreaId"" = m.""Id""
            WHERE b.""Date"" >= @since
            GROUP BY m.""Id"", m.""Name""
            HAVING MAX(b.""DegreeHeatingWeek"") > 4
            ORDER BY MAX(b.""DegreeHeatingWeek"") DESC
            LIMIT 10";

        try
        {
            var since = DateTime.UtcNow.AddDays(-14);
            var results = await _context.Database
                .SqlQueryRaw<BleachingHotspotResult>(sql,
                    new Npgsql.NpgsqlParameter("@since", since))
                .ToListAsync().ConfigureAwait(false);

            if (results.Count == 0)
                return "No significant bleaching hotspots detected in the past 14 days (DHW > 4)";

            var result = "Bleaching hotspots (past 14 days, DHW > 4):\n";
            foreach (var hotspot in results)
            {
                result += $"- {hotspot.MpaName}: DHW {hotspot.MaxDHW:F1}, {hotspot.AlertCount} alerts\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error executing query: {ex.Message}";
        }
    }

    // Result DTOs for raw SQL queries
    private class NearestMpaResult
    {
        public string Name { get; set; } = "";
        public string ProtectionLevel { get; set; } = "";
        public double AreaSquareKm { get; set; }
        public string IslandGroup { get; set; } = "";
        public double distance_km { get; set; }
    }

    private class MpaContainsResult
    {
        public string Name { get; set; } = "";
        public string ProtectionLevel { get; set; } = "";
        public double AreaSquareKm { get; set; }
        public string IslandGroup { get; set; } = "";
    }

    private class BleachingHotspotResult
    {
        public string MpaName { get; set; } = "";
        public double MaxDHW { get; set; }
        public string HighestAlert { get; set; } = "";
        public int AlertCount { get; set; }
    }
}
