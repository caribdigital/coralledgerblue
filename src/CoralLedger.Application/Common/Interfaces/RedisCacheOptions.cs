namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Configuration options for Redis distributed caching
/// </summary>
public class RedisCacheOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string (e.g., "localhost:6379")
    /// Can be overridden by REDIS_CONNECTION_STRING environment variable
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Instance name prefix for Redis keys (helps isolate different environments)
    /// </summary>
    public string InstanceName { get; set; } = "CoralLedger:";

    /// <summary>
    /// Enable Redis caching. If false, falls back to in-memory caching
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default TTL for MPA GeoJSON cache in hours
    /// </summary>
    public int MpaGeoJsonCacheTtlHours { get; set; } = 6;

    /// <summary>
    /// Default TTL for NOAA bleaching data cache in hours
    /// </summary>
    public int NoaaBleachingCacheTtlHours { get; set; } = 12;
}
