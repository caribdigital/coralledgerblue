using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Redis-based distributed cache implementation with prefix-based invalidation support.
/// Uses IDistributedCache for Azure Cache for Redis compatibility.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisCacheService(
        IDistributedCache cache,
        ILogger<RedisCacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _cache = cache;
        _logger = logger;
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var bytes = await _cache.GetAsync(key, ct).ConfigureAwait(false);
            if (bytes is not null)
            {
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                // Default: 30 minutes absolute expiration
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            }

            await _cache.SetAsync(key, bytes, options, ct).ConfigureAwait(false);
            _logger.LogDebug("Cache set for key: {Key}, Expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await _cache.RemoveAsync(key, ct).ConfigureAwait(false);
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            // If direct Redis access is available, use server-side key scanning
            if (_redis is not null)
            {
                var endpoints = _redis.GetEndPoints();
                if (endpoints.Length == 0)
                {
                    _logger.LogWarning("No Redis endpoints available for prefix-based cache removal");
                    return;
                }

                // Try to find an available server
                IServer? server = null;
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var candidate = _redis.GetServer(endpoint);
                        if (candidate.IsConnected)
                        {
                            server = candidate;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to connect to Redis endpoint: {Endpoint}", endpoint);
                    }
                }

                if (server is null)
                {
                    _logger.LogWarning("No available Redis server found for prefix-based cache removal");
                    return;
                }

                var pattern = $"{prefix}*";
                var keysToRemove = new List<RedisKey>();

                // Using SCAN-based iteration via KeysAsync for production-safe key scanning.
                // KeysAsync internally uses Redis SCAN command (for Redis 2.8+) which is non-blocking
                // and safe for production, only falling back to KEYS for legacy Redis versions.
                // The async enumeration handles cursor-based pagination automatically.
                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    keysToRemove.Add(key);
                }

                if (keysToRemove.Count > 0)
                {
                    // Use batch deletion for optimal performance
                    var db = _redis.GetDatabase();
                    var keyArray = keysToRemove.ToArray();
                    await db.KeyDeleteAsync(keyArray).ConfigureAwait(false);
                }

                _logger.LogDebug("Cache removed {Count} entries with prefix: {Prefix}", keysToRemove.Count, prefix);
            }
            else
            {
                // Fallback: Log warning that prefix-based removal is not supported without direct Redis access
                _logger.LogWarning("RemoveByPrefixAsync called but IConnectionMultiplexer not available. " +
                    "Prefix-based cache invalidation requires direct Redis connection. Prefix: {Prefix}", prefix);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entries with prefix: {Prefix}", prefix);
        }
    }

    /// <summary>
    /// Gets a value from cache or sets it using a factory function.
    /// Note: Unlike Get/SetAsync which gracefully handle failures, this method propagates
    /// factory exceptions to ensure data consistency - if the factory fails, the caller
    /// should know rather than getting a null/default value.
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            // Try to get from cache first
            var cached = await GetAsync<T>(key, ct).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            // Execute factory and cache result
            var value = await factory().ConfigureAwait(false);
            await SetAsync(key, value, expiration, ct).ConfigureAwait(false);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
            throw;
        }
    }
}
