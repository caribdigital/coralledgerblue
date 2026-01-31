using System.ComponentModel;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace CoralLedger.Blue.Infrastructure.AI.Plugins;

/// <summary>
/// Semantic Kernel plugin for querying marine data
/// </summary>
public class MarineDataPlugin
{
    private readonly IMarineDbContext _context;

    public MarineDataPlugin(IMarineDbContext context)
    {
        _context = context;
    }

    [KernelFunction("get_mpa_count")]
    [Description("Get the total count of Marine Protected Areas in the Bahamas")]
    public async Task<int> GetMpaCountAsync()
    {
        return await _context.MarineProtectedAreas.CountAsync().ConfigureAwait(false);
    }

    [KernelFunction("get_mpas_by_protection_level")]
    [Description("Get Marine Protected Areas filtered by protection level. Valid levels: NoTake, HighlyProtected, LightlyProtected, MinimalProtection")]
    public async Task<string> GetMpasByProtectionLevelAsync(
        [Description("Protection level to filter by")] string protectionLevel)
    {
        if (!Enum.TryParse<ProtectionLevel>(protectionLevel, true, out var level))
        {
            return $"Invalid protection level. Use: NoTake, HighlyProtected, LightlyProtected, or MinimalProtection";
        }

        var mpas = await _context.MarineProtectedAreas
            .Where(m => m.ProtectionLevel == level)
            .Select(m => new { m.Name, m.AreaSquareKm, m.IslandGroup })
            .ToListAsync().ConfigureAwait(false);

        if (mpas.Count == 0)
            return $"No MPAs found with protection level {protectionLevel}";

        var result = $"Found {mpas.Count} MPAs with {protectionLevel} protection:\n";
        foreach (var mpa in mpas.Take(10))
        {
            result += $"- {mpa.Name} ({mpa.AreaSquareKm:N0} km², {mpa.IslandGroup})\n";
        }
        if (mpas.Count > 10)
            result += $"... and {mpas.Count - 10} more";

        return result;
    }

    [KernelFunction("get_mpas_by_island_group")]
    [Description("Get Marine Protected Areas in a specific island group of the Bahamas")]
    public async Task<string> GetMpasByIslandGroupAsync(
        [Description("Island group name (e.g., Exumas, Andros, Abaco, Nassau, GrandBahama)")] string islandGroup)
    {
        if (!Enum.TryParse<IslandGroup>(islandGroup, true, out var group))
        {
            var validGroups = string.Join(", ", Enum.GetNames<IslandGroup>());
            return $"Invalid island group. Valid options: {validGroups}";
        }

        var mpas = await _context.MarineProtectedAreas
            .Where(m => m.IslandGroup == group)
            .Select(m => new { m.Name, m.ProtectionLevel, m.AreaSquareKm })
            .ToListAsync().ConfigureAwait(false);

        if (mpas.Count == 0)
            return $"No MPAs found in {islandGroup}";

        var result = $"Found {mpas.Count} MPAs in {islandGroup}:\n";
        foreach (var mpa in mpas)
        {
            result += $"- {mpa.Name} ({mpa.ProtectionLevel}, {mpa.AreaSquareKm:N0} km²)\n";
        }

        return result;
    }

    [KernelFunction("get_total_protected_area")]
    [Description("Get the total protected marine area in square kilometers")]
    public async Task<string> GetTotalProtectedAreaAsync()
    {
        var totalArea = await _context.MarineProtectedAreas
            .SumAsync(m => m.AreaSquareKm).ConfigureAwait(false);

        var byLevel = await _context.MarineProtectedAreas
            .GroupBy(m => m.ProtectionLevel)
            .Select(g => new { Level = g.Key, Area = g.Sum(m => m.AreaSquareKm) })
            .ToListAsync().ConfigureAwait(false);

        var result = $"Total protected marine area: {totalArea:N0} km²\n\nBy protection level:\n";
        foreach (var item in byLevel.OrderByDescending(x => x.Area))
        {
            result += $"- {item.Level}: {item.Area:N0} km²\n";
        }

        return result;
    }

    [KernelFunction("get_bleaching_alerts")]
    [Description("Get current coral bleaching alerts. Optional filter by alert level (NoStress, BleachingWatch, BleachingWarning, AlertLevel1-5)")]
    public async Task<string> GetBleachingAlertsAsync(
        [Description("Optional: minimum alert level to filter")] string? minAlertLevel = null)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var query = _context.BleachingAlerts
            .Include(b => b.MarineProtectedArea)
            .Where(b => b.Date >= cutoffDate)
            .AsQueryable();

        if (!string.IsNullOrEmpty(minAlertLevel) &&
            Enum.TryParse<BleachingAlertLevel>(minAlertLevel, true, out var level))
        {
            query = query.Where(b => b.AlertLevel >= level);
        }

        var alerts = await query
            .OrderByDescending(b => b.AlertLevel)
            .ThenByDescending(b => b.DegreeHeatingWeek)
            .Take(10)
            .Select(b => new
            {
                b.AlertLevel,
                b.DegreeHeatingWeek,
                b.SeaSurfaceTemperature,
                MpaName = b.MarineProtectedArea != null ? b.MarineProtectedArea.Name : "Unknown"
            })
            .ToListAsync().ConfigureAwait(false);

        if (alerts.Count == 0)
            return "No bleaching alerts in the past 7 days";

        var result = $"Found {alerts.Count} bleaching alerts (past 7 days):\n";
        foreach (var alert in alerts)
        {
            result += $"- {alert.MpaName}: {alert.AlertLevel} (DHW: {alert.DegreeHeatingWeek:F1}, SST: {alert.SeaSurfaceTemperature:F1}°C)\n";
        }

        return result;
    }

    [KernelFunction("get_fishing_activity")]
    [Description("Get recent fishing activity in Bahamas waters")]
    public async Task<string> GetFishingActivityAsync(
        [Description("Number of days to look back (default 7)")] int days = 7)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var events = await _context.VesselEvents
            .Include(e => e.Vessel)
            .Include(e => e.MarineProtectedArea)
            .Where(e => e.EventType == VesselEventType.Fishing)
            .Where(e => e.StartTime >= since)
            .OrderByDescending(e => e.StartTime)
            .Take(20)
            .Select(e => new
            {
                VesselName = e.Vessel.Name,
                e.StartTime,
                e.DurationHours,
                e.IsInMpa,
                MpaName = e.MarineProtectedArea != null ? e.MarineProtectedArea.Name : null
            })
            .ToListAsync().ConfigureAwait(false);

        if (events.Count == 0)
            return $"No fishing activity recorded in the past {days} days";

        var inMpaCount = events.Count(e => e.IsInMpa == true);
        var result = $"Found {events.Count} fishing events in past {days} days ({inMpaCount} inside MPAs):\n";

        foreach (var evt in events.Take(10))
        {
            var mpaInfo = evt.IsInMpa == true ? $" [IN MPA: {evt.MpaName}]" : "";
            result += $"- {evt.VesselName}: {evt.StartTime:MMM d} ({evt.DurationHours:F1}h){mpaInfo}\n";
        }

        return result;
    }

    [KernelFunction("get_reef_health_summary")]
    [Description("Get a summary of reef health across all monitored reefs")]
    public async Task<string> GetReefHealthSummaryAsync()
    {
        var reefs = await _context.Reefs
            .GroupBy(r => r.HealthStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync().ConfigureAwait(false);

        if (reefs.Count == 0)
            return "No reef health data available";

        var total = reefs.Sum(r => r.Count);
        var result = $"Reef health summary ({total} reefs monitored):\n";

        foreach (var item in reefs.OrderBy(r => r.Status))
        {
            var percentage = (double)item.Count / total * 100;
            result += $"- {item.Status}: {item.Count} ({percentage:F1}%)\n";
        }

        return result;
    }

    [KernelFunction("get_citizen_observations")]
    [Description("Get recent citizen science observations")]
    public async Task<string> GetCitizenObservationsAsync(
        [Description("Optional: filter by observation type")] string? observationType = null,
        [Description("Number of days to look back (default 30)")] int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var query = _context.CitizenObservations
            .Include(o => o.MarineProtectedArea)
            .Where(o => o.CreatedAt >= since)
            .AsQueryable();

        if (!string.IsNullOrEmpty(observationType) &&
            Enum.TryParse<ObservationType>(observationType, true, out var type))
        {
            query = query.Where(o => o.Type == type);
        }

        var observations = await query
            .OrderByDescending(o => o.ObservationTime)
            .Take(15)
            .Select(o => new
            {
                o.Title,
                o.Type,
                o.Severity,
                o.ObservationTime,
                o.Status,
                MpaName = o.MarineProtectedArea != null ? o.MarineProtectedArea.Name : null
            })
            .ToListAsync().ConfigureAwait(false);

        if (observations.Count == 0)
            return $"No citizen observations in the past {days} days";

        var result = $"Found {observations.Count} citizen observations (past {days} days):\n";
        foreach (var obs in observations)
        {
            var location = obs.MpaName != null ? $" in {obs.MpaName}" : "";
            result += $"- [{obs.Type}] {obs.Title} (Severity: {obs.Severity}/5){location}\n";
        }

        return result;
    }
}
