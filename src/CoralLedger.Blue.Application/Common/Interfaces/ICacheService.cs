namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Distributed cache service for performance optimization
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key.
    /// Returns null on both cache miss and cache errors (transparent failure).
    /// This design allows the cache to fail gracefully without affecting application logic.
    /// Errors are logged for monitoring but don't propagate to callers.
    /// </summary>
    /// <remarks>
    /// Note: This method intentionally does NOT use ServiceResult pattern because:
    /// 1. Cache is optional - applications should work without it
    /// 2. Cache miss (no data) and cache error (system unavailable) have the same outcome: fetch from source
    /// 3. Transparent failures prevent cache issues from breaking application features
    /// </remarks>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Set a cached value with optional expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Remove a cached value by key
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Remove all cached values matching a pattern
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Get or set a cached value using a factory function
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;
}

/// <summary>
/// Cache key constants for consistent naming
/// </summary>
public static class CacheKeys
{
    // MPA cache keys with versioning
    public const string MpaList = "mpas:list:v1";
    public const string MpaGeoJson = "mpas:geojson:{0}:v1"; // {0} = resolution (full/medium/low)
    public const string MpaStats = "mpas:stats:v1";
    public const string MpaDetail = "mpas:detail:{0}:v1"; // {0} = mpa id
    public const string MpaPrefix = "mpas:";

    // Bleaching cache keys with versioning
    public const string BleachingAlerts = "noaa:bleaching:alerts:v1";
    public const string BleachingLatest = "noaa:bleaching:{0}:v1"; // {0} = mpa id
    public const string BleachingRegion = "noaa:bleaching:region:{0}:{1}:{2}:v1"; // {0} = region hash, {1} = start date, {2} = end date
    public const string BleachingPoint = "noaa:bleaching:point:{0}:{1}:{2}:v1"; // {0} = lon, {1} = lat, {2} = date
    public const string BleachingTimeSeries = "noaa:bleaching:timeseries:{0}:{1}:{2}:{3}:v1"; // {0} = lon, {1} = lat, {2} = start date, {3} = end date
    public const string BleachingPrefix = "noaa:bleaching:";

    // Vessel cache keys
    public const string VesselPositions = "vessels:positions:v1";
    public const string VesselEvents = "vessels:events:v1";

    // Global Fishing Watch cache keys
    public const string GfwVesselSearch = "gfw:vessels:search:{0}:v1"; // {0} = query hash
    public const string GfwVesselDetail = "gfw:vessels:detail:{0}:v1"; // {0} = vessel id
    public const string GfwFishingEvents = "gfw:events:fishing:{0}:{1}:{2}:v1"; // {0} = region hash, {1} = start date, {2} = end date
    public const string GfwPortVisits = "gfw:events:port:{0}:{1}:{2}:v1"; // {0} = vessel id or "all", {1} = start date, {2} = end date
    public const string GfwEncounters = "gfw:events:encounters:{0}:{1}:{2}:v1"; // {0} = region hash, {1} = start date, {2} = end date
    public const string GfwFishingStats = "gfw:stats:fishing:{0}:{1}:{2}:v1"; // {0} = region hash, {1} = start date, {2} = end date
    public const string GfwPrefix = "gfw:";

    // Alert cache keys
    public const string AlertsActive = "alerts:active:v1";
    public const string AlertRules = "alerts:rules:v1";

    // Dashboard cache keys
    public const string DashboardStats = "admin:dashboard:v1";

    // Helper methods for key generation
    public static string ForMpa(Guid id) => string.Format(MpaDetail, id);
    public static string ForMpaGeoJson(string resolution) => string.Format(MpaGeoJson, resolution.ToLowerInvariant());
    public static string ForBleaching(Guid mpaId) => string.Format(BleachingLatest, mpaId);
    public static string ForBleachingPoint(double lon, double lat, DateOnly date) =>
        string.Format(BleachingPoint, lon.ToString("F6"), lat.ToString("F6"), date.ToString("yyyy-MM-dd"));
    public static string ForBleachingRegion(double minLon, double minLat, double maxLon, double maxLat, DateOnly startDate, DateOnly endDate)
    {
        var regionHash = $"{minLon:F2}_{minLat:F2}_{maxLon:F2}_{maxLat:F2}".GetHashCode().ToString("X");
        return string.Format(BleachingRegion, regionHash, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
    }
    public static string ForBleachingTimeSeries(double lon, double lat, DateOnly startDate, DateOnly endDate) =>
        string.Format(BleachingTimeSeries, lon.ToString("F6"), lat.ToString("F6"), startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

    // GFW helper methods
    public static string ForGfwVesselSearch(string? query, string? flag, string? vesselType)
    {
        var hash = $"{query ?? ""}:{flag ?? ""}:{vesselType ?? ""}".GetHashCode().ToString("X");
        return string.Format(GfwVesselSearch, hash);
    }

    public static string ForGfwVessel(string vesselId) => string.Format(GfwVesselDetail, vesselId);

    public static string ForGfwFishingEvents(double minLon, double minLat, double maxLon, double maxLat, DateTime startDate, DateTime endDate)
    {
        var regionHash = $"{minLon:F2}_{minLat:F2}_{maxLon:F2}_{maxLat:F2}".GetHashCode().ToString("X");
        return string.Format(GfwFishingEvents, regionHash, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
    }

    public static string ForGfwPortVisits(string? vesselId, DateTime? startDate, DateTime? endDate)
    {
        var vessel = vesselId ?? "all";
        var start = startDate?.ToString("yyyy-MM-dd") ?? "any";
        var end = endDate?.ToString("yyyy-MM-dd") ?? "any";
        return string.Format(GfwPortVisits, vessel, start, end);
    }

    public static string ForGfwEncounters(double minLon, double minLat, double maxLon, double maxLat, DateTime startDate, DateTime endDate)
    {
        var regionHash = $"{minLon:F2}_{minLat:F2}_{maxLon:F2}_{maxLat:F2}".GetHashCode().ToString("X");
        return string.Format(GfwEncounters, regionHash, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
    }

    public static string ForGfwFishingStats(double minLon, double minLat, double maxLon, double maxLat, DateTime startDate, DateTime endDate)
    {
        var regionHash = $"{minLon:F2}_{minLat:F2}_{maxLon:F2}_{maxLat:F2}".GetHashCode().ToString("X");
        return string.Format(GfwFishingStats, regionHash, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
    }
}

/// <summary>
/// Cache duration presets
/// </summary>
public static class CacheDurations
{
    public static readonly TimeSpan VeryShort = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Long = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan VeryLong = TimeSpan.FromHours(1);
    public static readonly TimeSpan Day = TimeSpan.FromDays(1);
}
