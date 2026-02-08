# Redis Distributed Caching

CoralLedger Blue uses Redis for distributed caching to improve performance and scalability. This document explains the Redis integration, configuration, and deployment options.

## Overview

The application uses Redis to cache:
- **MPA GeoJSON boundaries** (6 hours TTL by default)
- **NOAA coral bleaching data** (12 hours TTL by default)
- **Spatial analysis results** (15 minutes TTL)

## Local Development Setup

### Option 1: Using Docker (Recommended)

The easiest way to run Redis locally is using Docker:

```bash
docker run -d -p 6379:6379 --name coralledger-redis redis:7-alpine
```

The application is pre-configured to connect to `localhost:6379` in development mode.

### Option 2: Install Redis Locally

**Windows (using Chocolatey):**
```powershell
choco install redis-64
redis-server
```

**macOS (using Homebrew):**
```bash
brew install redis
brew services start redis
```

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install redis-server
sudo systemctl start redis-server
```

## Configuration

### appsettings.json

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "CoralLedger:",
    "Enabled": true,
    "MpaGeoJsonCacheTtlHours": 6,
    "NoaaBleachingCacheTtlHours": 12
  }
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `ConnectionString` | Redis server connection string | `localhost:6379` |
| `InstanceName` | Key prefix for Redis entries (helps isolate environments) | `CoralLedger:` |
| `Enabled` | Enable/disable Redis caching (falls back to in-memory if false) | `true` |
| `MpaGeoJsonCacheTtlHours` | Cache duration for MPA GeoJSON data (hours) | `6` |
| `NoaaBleachingCacheTtlHours` | Cache duration for NOAA bleaching data (hours) | `12` |

### Environment Variables

You can override the Redis connection string using an environment variable:

```bash
export REDIS_CONNECTION_STRING="your-redis-server:6379"
```

Or on Windows:
```powershell
$env:REDIS_CONNECTION_STRING="your-redis-server:6379"
```

## Azure Cache for Redis (Production)

For production deployment on Azure, use Azure Cache for Redis:

### 1. Create Azure Cache for Redis

```bash
az redis create \
  --name coralledger-redis \
  --resource-group coralledger-rg \
  --location eastus \
  --sku Basic \
  --vm-size C1
```

### 2. Get Connection String

```bash
az redis list-keys \
  --name coralledger-redis \
  --resource-group coralledger-rg
```

### 3. Configure Application

**Option A: Environment Variable (Recommended)**

Set the `REDIS_CONNECTION_STRING` environment variable in your Azure App Service:

```bash
az webapp config appsettings set \
  --name coralledger-web \
  --resource-group coralledger-rg \
  --settings REDIS_CONNECTION_STRING="coralledger-redis.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True,abortConnect=False"
```

**Option B: App Configuration**

Use Azure App Configuration to manage the connection string securely.

### 4. SSL Configuration

Azure Cache for Redis requires SSL. Ensure your connection string includes `ssl=True`:

```
your-cache.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True,abortConnect=False
```

## Cache Keys Structure

The application uses versioned cache keys to allow cache invalidation during updates:

### MPA GeoJSON Cache
```
mpas:geojson:{resolution}:v1
```
Examples:
- `mpas:geojson:medium:v1`
- `mpas:geojson:full:v1`
- `mpas:geojson:low:v1`

### NOAA Bleaching Data Cache
```
noaa:bleaching:point:{lon}:{lat}:{date}:v1
noaa:bleaching:region:{regionHash}:{startDate}:{endDate}:v1
noaa:bleaching:timeseries:{lon}:{lat}:{startDate}:{endDate}:v1
```

### MPA Detail Cache
```
mpas:detail:{mpaId}:v1
```

## Cache Invalidation

Cache is automatically invalidated when:

1. **MPA boundaries are updated** via WDPA sync
   - All MPA GeoJSON cache entries are cleared
   - Specific MPA detail cache is removed

2. **Manual invalidation** via API endpoints (coming soon)

### Prefix-Based Cache Invalidation

The `RedisCacheService` supports efficient prefix-based cache invalidation using Redis SCAN:

```csharp
// Remove all cache entries starting with "mpas:"
await cacheService.RemoveByPrefixAsync("mpas:");
```

**Implementation Details:**
- Uses Redis **SCAN command** (for Redis 2.8+) for production-safe, non-blocking key iteration via [StackExchange.Redis KeysAsync](https://stackexchange.github.io/StackExchange.Redis/KeysScan.html)
- Only falls back to KEYS command for legacy Redis versions (< 2.8)
- Handles pagination automatically through cursor-based iteration
- Safe for production use with millions of keys
- Requires `IConnectionMultiplexer` to be registered in DI container
- **Fallback behavior**: If `IConnectionMultiplexer` is not available, a warning is logged and the operation is skipped (no keys are removed)

**Configuration:**
```csharp
services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnectionString));
services.AddStackExchangeRedisCache(options => 
{
    options.Configuration = redisConnectionString;
});
```

**Performance Characteristics:**
- **Time Complexity**: O(N) where N is the number of keys in the database
- **Blocking**: Non-blocking (uses SCAN, not KEYS)
- **Network**: Multiple round trips (cursor-based pagination)
- **Memory**: Minimal - keys are processed in batches

**Best Practices:**
- Use specific prefixes to minimize keys scanned (e.g., `"mpas:detail:123:"` instead of `"mpas:"`)
- Consider cache versioning (e.g., `v1`, `v2`) for breaking changes instead of prefix removal
- Monitor SCAN operation duration in production for performance insights

## Fallback Behavior

If Redis is unavailable or disabled:
- The application falls back to in-memory caching using `IMemoryCache`
- No data loss occurs; the application continues to function
- Performance may be reduced for high-traffic scenarios

## Monitoring & Troubleshooting

### Check Redis Connection

```bash
redis-cli -h localhost -p 6379 ping
# Should return: PONG
```

### View Cached Keys

```bash
redis-cli -h localhost -p 6379
> KEYS CoralLedger:*
```

### Clear Cache

```bash
redis-cli -h localhost -p 6379
> FLUSHDB
```

### Monitor Cache Activity

```bash
redis-cli -h localhost -p 6379 MONITOR
```

## Health Checks

The application includes a Redis health check available at `/health`:

```bash
curl https://your-app.com/health
```

Response includes cache status:
```json
{
  "status": "Healthy",
  "results": {
    "cache": {
      "status": "Healthy",
      "description": "Redis cache is responsive"
    }
  }
}
```

## Azure Environment Strategy

### Dev Environment — Build on Demand, Delete When Idle

Azure Cache for Redis has no stopped/paused state — instances incur cost 24/7 regardless of usage. To avoid unnecessary spend, the **dev Redis instance should be created only when actively developing and deleted when idle**.

**Create dev Redis (when starting dev work):**

```bash
az redis create \
  --name redis-coralcomply-dev \
  --resource-group rg-coralcomply-dev \
  --location eastus2 \
  --sku Basic \
  --vm-size c0 \
  --minimum-tls-version 1.2 \
  --redis-configuration '{"maxmemory-policy":"volatile-lru"}' \
  --tags Application=coralledgerblue Environment=dev ManagedBy=Bicep
```

> **Note:** Redis provisioning takes ~10–15 minutes. Plan accordingly at the start of a dev session.

**Delete dev Redis (when done for the day/sprint):**

```bash
az redis delete \
  --name redis-coralcomply-dev \
  --resource-group rg-coralcomply-dev \
  --yes
```

**Local alternative:** For quick local development, use Docker instead of Azure Redis:

```bash
docker run -d -p 6379:6379 --name coralledger-redis redis:7-alpine
```

The application falls back to `IMemoryCache` automatically if Redis is unavailable, so dev work is never blocked by the absence of a Redis instance.

### Staging — Basic C0

Staging uses **Basic C0** (250 MB, no SLA, no replica). This is sufficient for integration testing and costs ~$14/month.

- Instance: `redis-coralcomply-stg`
- Resource group: `rg-coralcomply-stg`

### Production — Standard C0

Production uses **Standard C0** (250 MB, SLA-backed with replica). This provides high availability at ~$35/month. Monitor memory usage — if cache utilization consistently exceeds 200 MB, consider scaling to Standard C1.

- Instance: `redis-coralcomply-prd`
- Resource group: `rg-coralcomply-prd`

## Performance Considerations

### Cache Sizing

Current tier allocations (as of February 2026):
- **Development**: Created on demand using Basic C0 (250 MB) — ~$14/month when running
- **Staging**: Basic C0 (250 MB) — ~$14/month
- **Production**: Standard C0 (250 MB, with replica) — ~$35/month
- **High Traffic (future)**: Standard C1 (1 GB) — ~$86/month

### Key Expiration

Keys automatically expire based on configured TTLs:
- MPA GeoJSON: 6 hours (configurable)
- NOAA Bleaching: 12 hours (configurable)
- Spatial Analysis: 15 minutes (hardcoded)

### Memory Optimization

The application uses JSON serialization with camelCase for compact storage:
- Average MPA GeoJSON (medium resolution): ~50 KB
- Average NOAA bleaching response: ~5 KB
- Estimated cache size for 8 MPAs + 7 days of bleaching data: ~2 MB

## Migration from In-Memory Cache

If you're upgrading from a version using in-memory caching:

1. **No code changes required** - The cache service interface (`ICacheService`) remains the same
2. **Update configuration** - Add Redis configuration to `appsettings.json`
3. **Deploy Redis** - Set up Redis server or Azure Cache for Redis
4. **Test thoroughly** - Verify cache behavior in staging environment
5. **Monitor performance** - Watch for improvements in response times

## Troubleshooting

### Connection Refused

**Symptom**: `Connection refused` errors in logs

**Solution**: 
- Check Redis is running: `redis-cli ping`
- Verify connection string in configuration
- Check firewall rules (Azure NSG, local firewall)

### SSL/TLS Errors

**Symptom**: SSL/TLS errors when connecting to Azure Cache for Redis

**Solution**:
- Ensure connection string includes `ssl=True`
- Use port 6380 (not 6379) for Azure
- Add `abortConnect=False` to connection string

### High Memory Usage

**Symptom**: Redis memory usage grows continuously

**Solution**:
- Verify TTL values are set correctly
- Check for keys without expiration: `redis-cli KEYS * | xargs redis-cli TTL`
- Consider increasing cache size or reducing TTL values

### Performance Issues

**Symptom**: Slow cache operations

**Solution**:
- Check network latency to Redis server
- Monitor Redis CPU usage
- Consider upgrading Redis tier (Azure)
- Review connection pool settings

## Further Reading

- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [Azure Cache for Redis Best Practices](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/cache-best-practices)
- [Redis Documentation](https://redis.io/documentation)

## Support

For issues or questions:
- Open an issue on [GitHub](https://github.com/caribdigital/coralledgerblue/issues)
- Review existing [discussions](https://github.com/caribdigital/coralledgerblue/discussions)
- Contact the maintainers at [DigitalCarib.com](https://digitalcarib.com)
