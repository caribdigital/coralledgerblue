using System.Collections.Concurrent;
using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// In-memory cache implementation with prefix-based invalidation support.
/// For production, consider using Redis via IDistributedCache.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return Task.FromResult(value);
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiration;
        }
        else
        {
            // Default: 5 minutes sliding, 30 minutes absolute
            options.SlidingExpiration = TimeSpan.FromMinutes(5);
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
        }

        // Track eviction for key management
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            _keys.TryRemove(evictedKey.ToString()!, out _);
            _logger.LogDebug("Cache entry evicted: {Key}", evictedKey);
        });

        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);

        _logger.LogDebug("Cache set for key: {Key}, Expiration: {Expiration}", key, expiration);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _cache.Remove(key);
        _keys.TryRemove(key, out _);

        _logger.LogDebug("Cache removed for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var keysToRemove = _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        _logger.LogDebug("Cache removed {Count} entries with prefix: {Prefix}", keysToRemove.Count, prefix);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();

        // Try to get from cache first
        var cached = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        // Use semaphore to prevent cache stampede
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            cached = await GetAsync<T>(key, ct).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            // Execute factory and cache result
            var value = await factory().ConfigureAwait(false);
            await SetAsync(key, value, expiration, ct).ConfigureAwait(false);
            return value;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
