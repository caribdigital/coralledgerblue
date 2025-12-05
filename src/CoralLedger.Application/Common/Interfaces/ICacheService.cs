namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Distributed cache service for performance optimization
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key
    /// </summary>
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
