using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoralLedger.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoralLedger.Infrastructure.ExternalServices;

/// <summary>
/// Client implementation for NOAA Coral Reef Watch ERDDAP data
/// Base URL: https://coastwatch.pfeg.noaa.gov/erddap/griddap/
/// Dataset: NOAA_DHW (5km daily bleaching monitoring products)
/// </summary>
public class CoralReefWatchClient : ICoralReefWatchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoralReefWatchClient> _logger;
    private readonly ICacheService _cache;
    private readonly Microsoft.Extensions.Options.IOptions<RedisCacheOptions> _cacheOptions;
    private readonly CoralReefWatchOptions _options;
    private readonly IReadOnlyList<CrwBleachingData>? _mockData;

    // NOAA ERDDAP base URL for Coral Reef Watch DHW dataset
    private const string ErddapBaseUrl = "https://coastwatch.pfeg.noaa.gov/erddap/griddap/";
    private const string DhwDataset = "NOAA_DHW";

    // Bahamas bounding box (approximate)
    private const double BahamasMinLon = -80.5;
    private const double BahamasMaxLon = -72.5;
    private const double BahamasMinLat = 20.5;
    private const double BahamasMaxLat = 27.5;

    private readonly JsonSerializerOptions _jsonOptions;

    public CoralReefWatchClient(
        HttpClient httpClient,
        ILogger<CoralReefWatchClient> logger,
        ICacheService cache,
        Microsoft.Extensions.Options.IOptions<RedisCacheOptions> cacheOptions,
        IOptions<CoralReefWatchOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = cacheOptions;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(ErddapBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // Reduced for better UX

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        if (_options.UseMockData)
        {
            var mockPath = Path.Combine(AppContext.BaseDirectory, _options.MockDataPath);
            if (File.Exists(mockPath))
            {
                _mockData = JsonSerializer.Deserialize<List<CrwBleachingData>>(
                    File.ReadAllText(mockPath), _jsonOptions) ?? new List<CrwBleachingData>();
                _logger.LogInformation("Loaded {Count} mock bleaching records from {Path}", _mockData.Count, mockPath);
            }
            else
            {
                _mockData = Array.Empty<CrwBleachingData>();
                _logger.LogWarning("Mock bleaching data not found at {Path}", mockPath);
            }
        }
    }

    public async Task<CrwBleachingData?> GetBleachingDataAsync(
        double longitude,
        double latitude,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (TryGetMockData(out var mockData))
        {
            return mockData.FirstOrDefault(record => MatchesPoint(record, longitude, latitude, date));
        }

        // Use cache with point-specific key
        var cacheKey = CacheKeys.ForBleachingPoint(longitude, latitude, date);
        var cacheTtl = TimeSpan.FromHours(_cacheOptions.Value.NoaaBleachingCacheTtlHours);

        // Try to get from cache first
        var cached = await _cache.GetAsync<CrwBleachingDataWrapper>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Data;
        }

        // Fetch fresh data
        var data = await FetchBleachingDataAsync(longitude, latitude, date, cancellationToken);
        
        // Cache the result (even if null, wrap it)
        if (data is not null)
        {
            await _cache.SetAsync(cacheKey, new CrwBleachingDataWrapper { Data = data }, cacheTtl, cancellationToken);
        }

        return data;
    }

    private async Task<CrwBleachingData?> FetchBleachingDataAsync(
        double longitude,
        double latitude,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ERDDAP query for single point: snap to nearest grid cell (0.05 degree resolution)
            var snapLon = Math.Round(longitude / 0.05) * 0.05;
            var snapLat = Math.Round(latitude / 0.05) * 0.05;
            var dateStr = date.ToString("yyyy-MM-dd");

            // Request all variables for the specific location and date
            var query = $"{DhwDataset}.json?" +
                        $"CRW_SST[({dateStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_SSTANOMALY[({dateStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_HOTSPOT[({dateStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_DHW[({dateStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_BAA[({dateStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]";

            var response = await _httpClient.GetAsync(query, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ERDDAP request failed with status {Status} for location ({Lon}, {Lat}) on {Date}",
                    response.StatusCode, longitude, latitude, date);
                return null;
            }

            var result = await ParseErddapResponseAsync(response, cancellationToken);
            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bleaching data for ({Lon}, {Lat}) on {Date}",
                longitude, latitude, date);
            return null;
        }
    }

    public async Task<IEnumerable<CrwBleachingData>> GetBleachingDataForRegionAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (TryGetMockData(out var mockData))
        {
            return mockData
                .Where(record =>
                    record.Longitude >= minLon &&
                    record.Longitude <= maxLon &&
                    record.Latitude >= minLat &&
                    record.Latitude <= maxLat &&
                    record.Date >= startDate &&
                    record.Date <= endDate)
                .ToList();
        }

        // Use cache with region-specific key
        var cacheKey = CacheKeys.ForBleachingRegion(minLon, minLat, maxLon, maxLat, startDate, endDate);
        var cacheTtl = TimeSpan.FromHours(_cacheOptions.Value.NoaaBleachingCacheTtlHours);

        // Try to get from cache first
        var cached = await _cache.GetAsync<CrwBleachingDataCollectionWrapper>(cacheKey, cancellationToken);
        if (cached?.Data is not null)
        {
            return cached.Data;
        }

        // Fetch fresh data
        var data = await FetchBleachingDataForRegionAsync(minLon, minLat, maxLon, maxLat, startDate, endDate, cancellationToken);
        
        // Cache the result
        await _cache.SetAsync(cacheKey, new CrwBleachingDataCollectionWrapper { Data = data }, cacheTtl, cancellationToken);

        return data;
    }

    private async Task<IEnumerable<CrwBleachingData>> FetchBleachingDataForRegionAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For large regions, we need to subsample to avoid huge responses
            // ERDDAP supports stride parameter for this
            var lonStride = Math.Max(1, (int)Math.Ceiling((maxLon - minLon) / 0.05 / 50));
            var latStride = Math.Max(1, (int)Math.Ceiling((maxLat - minLat) / 0.05 / 50));

            var startStr = startDate.ToString("yyyy-MM-dd");
            var endStr = endDate.ToString("yyyy-MM-dd");

            var query = $"{DhwDataset}.json?" +
                        $"CRW_SST[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({minLat}):{latStride}:({maxLat})][({minLon}):{lonStride}:({maxLon})]," +
                        $"CRW_SSTANOMALY[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({minLat}):{latStride}:({maxLat})][({minLon}):{lonStride}:({maxLon})]," +
                        $"CRW_HOTSPOT[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({minLat}):{latStride}:({maxLat})][({minLon}):{lonStride}:({maxLon})]," +
                        $"CRW_DHW[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({minLat}):{latStride}:({maxLat})][({minLon}):{lonStride}:({maxLon})]," +
                        $"CRW_BAA[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({minLat}):{latStride}:({maxLat})][({minLon}):{lonStride}:({maxLon})]";

            var response = await _httpClient.GetAsync(query, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await ParseErddapResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bleaching data for region");
            return Enumerable.Empty<CrwBleachingData>();
        }
    }

    public async Task<IEnumerable<CrwBleachingData>> GetBahamasBleachingAlertsAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)); // Yesterday to ensure data availability

        return await GetBleachingDataForRegionAsync(
            BahamasMinLon,
            BahamasMinLat,
            BahamasMaxLon,
            BahamasMaxLat,
            targetDate,
            targetDate,
            cancellationToken);
    }

    public async Task<IEnumerable<CrwBleachingData>> GetBleachingTimeSeriesAsync(
        double longitude,
        double latitude,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (TryGetMockData(out var mockData))
        {
            return mockData
                .Where(record =>
                    IsSameLocation(record, longitude, latitude) &&
                    record.Date >= startDate &&
                    record.Date <= endDate)
                .OrderBy(record => record.Date)
                .ToList();
        }

        // Use cache with time series-specific key
        var cacheKey = CacheKeys.ForBleachingTimeSeries(longitude, latitude, startDate, endDate);
        var cacheTtl = TimeSpan.FromHours(_cacheOptions.Value.NoaaBleachingCacheTtlHours);

        // Try to get from cache first
        var cached = await _cache.GetAsync<CrwBleachingDataCollectionWrapper>(cacheKey, cancellationToken);
        if (cached?.Data is not null)
        {
            return cached.Data;
        }

        // Fetch fresh data
        var data = await FetchBleachingTimeSeriesAsync(longitude, latitude, startDate, endDate, cancellationToken);
        
        // Cache the result
        await _cache.SetAsync(cacheKey, new CrwBleachingDataCollectionWrapper { Data = data }, cacheTtl, cancellationToken);

        return data;
    }

    private async Task<IEnumerable<CrwBleachingData>> FetchBleachingTimeSeriesAsync(
        double longitude,
        double latitude,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapLon = Math.Round(longitude / 0.05) * 0.05;
            var snapLat = Math.Round(latitude / 0.05) * 0.05;
            var startStr = startDate.ToString("yyyy-MM-dd");
            var endStr = endDate.ToString("yyyy-MM-dd");

            var query = $"{DhwDataset}.json?" +
                        $"CRW_SST[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_SSTANOMALY[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_HOTSPOT[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_DHW[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]," +
                        $"CRW_BAA[({startStr}T12:00:00Z):1:({endStr}T12:00:00Z)][({snapLat}):1:({snapLat})][({snapLon}):1:({snapLon})]";

            var response = await _httpClient.GetAsync(query, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await ParseErddapResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bleaching time series for ({Lon}, {Lat})",
                longitude, latitude);
            return Enumerable.Empty<CrwBleachingData>();
        }
    }

    private async Task<IEnumerable<CrwBleachingData>> ParseErddapResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var results = new List<CrwBleachingData>();

        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var table = doc.RootElement.GetProperty("table");
            var columnNames = table.GetProperty("columnNames").EnumerateArray()
                .Select(x => x.GetString() ?? "").ToList();
            var rows = table.GetProperty("rows").EnumerateArray().ToList();

            // Find column indices
            var timeIdx = columnNames.IndexOf("time");
            var latIdx = columnNames.IndexOf("latitude");
            var lonIdx = columnNames.IndexOf("longitude");
            var sstIdx = columnNames.IndexOf("CRW_SST");
            var anomalyIdx = columnNames.IndexOf("CRW_SSTANOMALY");
            var hotspotIdx = columnNames.IndexOf("CRW_HOTSPOT");
            var dhwIdx = columnNames.IndexOf("CRW_DHW");
            var baaIdx = columnNames.IndexOf("CRW_BAA");

            foreach (var row in rows)
            {
                var values = row.EnumerateArray().ToList();

                // Parse time (ISO format)
                var timeStr = values[timeIdx].GetString() ?? "";
                if (!DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
                    continue;

                // Get values, handling NaN as null for optional fields
                var sst = GetDoubleValue(values[sstIdx]);
                var anomaly = GetDoubleValue(values[anomalyIdx]);
                var dhw = GetDoubleValue(values[dhwIdx]);
                var hotspot = GetNullableDoubleValue(values[hotspotIdx]);
                var baa = GetIntValue(values[baaIdx]);

                if (!sst.HasValue || !anomaly.HasValue || !dhw.HasValue)
                    continue;

                results.Add(new CrwBleachingData
                {
                    Longitude = GetDoubleValue(values[lonIdx]) ?? 0,
                    Latitude = GetDoubleValue(values[latIdx]) ?? 0,
                    Date = DateOnly.FromDateTime(dateTime),
                    SeaSurfaceTemperature = sst.Value,
                    SstAnomaly = anomaly.Value,
                    HotSpot = hotspot > 0 ? hotspot : null,
                    DegreeHeatingWeek = dhw.Value,
                    AlertLevel = baa ?? 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ERDDAP response");
        }

        return results;
    }

    private bool TryGetMockData(out IReadOnlyList<CrwBleachingData> data)
    {
        data = _mockData ?? Array.Empty<CrwBleachingData>();
        return _options.UseMockData && data.Count > 0;
    }

    private static bool MatchesPoint(CrwBleachingData record, double longitude, double latitude, DateOnly date)
        => IsSameLocation(record, longitude, latitude) && record.Date == date;

    private static bool IsSameLocation(CrwBleachingData record, double longitude, double latitude)
    {
        const double tolerance = 0.5;
        return Math.Abs(record.Longitude - longitude) <= tolerance &&
               Math.Abs(record.Latitude - latitude) <= tolerance;
    }

    private static double? GetDoubleValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;

        if (element.ValueKind == JsonValueKind.Number)
        {
            var value = element.GetDouble();
            return double.IsNaN(value) ? null : value;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (string.IsNullOrEmpty(str) || str.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                return null;
            if (double.TryParse(str, CultureInfo.InvariantCulture, out var value))
                return value;
        }

        return null;
    }

    private static double? GetNullableDoubleValue(JsonElement element) => GetDoubleValue(element);

    private static int? GetIntValue(JsonElement element)
    {
        var d = GetDoubleValue(element);
        return d.HasValue ? (int)d.Value : null;
    }
}

/// <summary>
/// Generic wrapper class for caching data types, including nullable values and collections.
/// 
/// Note: These wrappers are necessary because:
/// 1. IDistributedCache requires non-null values for serialization
/// 2. IEnumerable<T> needs wrapping to serialize correctly as JSON
/// 3. Nullable reference types (T?) don't satisfy the 'where T : class' constraint
/// 
/// While this adds some complexity, it provides type-safe caching with proper null handling.
/// </summary>
internal class CacheWrapper<T>
{
    public T? Data { get; set; }
}

/// <summary>
/// Wrapper class for caching nullable CrwBleachingData
/// For backward compatibility with existing cache keys
/// </summary>
internal class CrwBleachingDataWrapper : CacheWrapper<CrwBleachingData>
{
}

/// <summary>
/// Wrapper class for caching collections of CrwBleachingData
/// For backward compatibility with existing cache keys
/// </summary>
internal class CrwBleachingDataCollectionWrapper : CacheWrapper<IEnumerable<CrwBleachingData>>
{
    public CrwBleachingDataCollectionWrapper()
    {
        Data = Enumerable.Empty<CrwBleachingData>();
    }
}
