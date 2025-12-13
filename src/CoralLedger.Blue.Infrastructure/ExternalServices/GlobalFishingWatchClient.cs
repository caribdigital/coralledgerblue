using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoralLedger.Blue.Infrastructure.ExternalServices;

/// <summary>
/// Client implementation for Global Fishing Watch API v3
/// Base URL: https://gateway.api.globalfishingwatch.org/
/// Implements Redis caching for API responses (Sprint 3.3 - US-3.3.5)
/// </summary>
public class GlobalFishingWatchClient : IGlobalFishingWatchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GlobalFishingWatchClient> _logger;
    private readonly ICacheService _cache;
    private readonly IOptions<RedisCacheOptions> _cacheOptions;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly GlobalFishingWatchOptions _options;

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrEmpty(_options.ApiToken);

    public GlobalFishingWatchClient(
        HttpClient httpClient,
        IOptions<GlobalFishingWatchOptions> options,
        ILogger<GlobalFishingWatchClient> logger,
        ICacheService cache,
        IOptions<RedisCacheOptions> cacheOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _cacheOptions = cacheOptions;
        _options = options.Value;

        // Validate configuration and warn if enabled but missing token
        if (_options.Enabled && string.IsNullOrEmpty(_options.ApiToken))
        {
            _logger.LogWarning(
                "GlobalFishingWatch is enabled but ApiToken is not configured. " +
                "API calls will fail. Set it using: " +
                "dotnet user-secrets set \"GlobalFishingWatch:ApiToken\" \"your-token\" --project src/CoralLedger.Blue.Web");
        }

        _httpClient.BaseAddress = new Uri("https://gateway.api.globalfishingwatch.org/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiToken);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    private TimeSpan CacheTtl => TimeSpan.FromHours(_cacheOptions.Value.GfwCacheTtlHours);

    public async Task<IEnumerable<GfwVesselInfo>> SearchVesselsAsync(
        string? query = null,
        string? flag = null,
        VesselType? vesselType = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = CacheKeys.ForGfwVesselSearch(query, flag, vesselType?.ToString());
        var cached = await _cache.GetAsync<GfwVesselCollectionWrapper>(cacheKey, cancellationToken);
        if (cached?.Data != null)
        {
            _logger.LogDebug("GFW vessel search cache hit for query: {Query}", query);
            return cached.Data;
        }

        try
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(query))
                queryParams.Add($"query={Uri.EscapeDataString(query)}");
            if (!string.IsNullOrEmpty(flag))
                queryParams.Add($"flag={flag}");
            if (vesselType.HasValue)
                queryParams.Add($"vessel-types={MapVesselType(vesselType.Value)}");

            queryParams.Add($"limit={limit}");
            queryParams.Add("datasets=public-global-vessel-identity:latest");

            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"v3/vessels/search?{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GfwVesselSearchResponse>(_jsonOptions, cancellationToken);
            var vessels = result?.Entries?.Select(MapToVesselInfo).ToList() ?? new List<GfwVesselInfo>();

            // Cache the result
            await _cache.SetAsync(cacheKey, new GfwVesselCollectionWrapper { Data = vessels }, CacheTtl, cancellationToken);

            return vessels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vessels with query: {Query}", query);
            return Enumerable.Empty<GfwVesselInfo>();
        }
    }

    public async Task<GfwVesselInfo?> GetVesselByIdAsync(
        string vesselId,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = CacheKeys.ForGfwVessel(vesselId);
        var cached = await _cache.GetAsync<GfwVesselWrapper>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("GFW vessel detail cache hit for: {VesselId}", vesselId);
            return cached.Data;
        }

        try
        {
            var response = await _httpClient.GetAsync(
                $"v3/vessels/{vesselId}?datasets=public-global-vessel-identity:latest",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vessel not found: {VesselId}", vesselId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GfwVesselEntry>(_jsonOptions, cancellationToken);
            var vessel = result != null ? MapToVesselInfo(result) : null;

            // Cache the result (even if null)
            if (vessel != null)
            {
                await _cache.SetAsync(cacheKey, new GfwVesselWrapper { Data = vessel }, CacheTtl, cancellationToken);
            }

            return vessel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vessel: {VesselId}", vesselId);
            return null;
        }
    }

    public async Task<IEnumerable<GfwEvent>> GetFishingEventsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = CacheKeys.ForGfwFishingEvents(minLon, minLat, maxLon, maxLat, startDate, endDate);
        var cached = await _cache.GetAsync<GfwEventCollectionWrapper>(cacheKey, cancellationToken);
        if (cached?.Data != null)
        {
            _logger.LogDebug("GFW fishing events cache hit for region");
            return cached.Data;
        }

        var events = await GetEventsAsync("fishing", minLon, minLat, maxLon, maxLat, startDate, endDate, limit, cancellationToken);
        var eventList = events.ToList();

        // Cache the result
        await _cache.SetAsync(cacheKey, new GfwEventCollectionWrapper { Data = eventList }, CacheTtl, cancellationToken);

        return eventList;
    }

    public async Task<IEnumerable<GfwEvent>> GetPortVisitsAsync(
        string? vesselId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = CacheKeys.ForGfwPortVisits(vesselId, startDate, endDate);
        var cached = await _cache.GetAsync<GfwEventCollectionWrapper>(cacheKey, cancellationToken);
        if (cached?.Data != null)
        {
            _logger.LogDebug("GFW port visits cache hit for vessel: {VesselId}", vesselId);
            return cached.Data;
        }

        try
        {
            var queryParams = new List<string>
            {
                "datasets=public-global-port-visits-events:latest",
                $"limit={limit}"
            };

            if (!string.IsNullOrEmpty(vesselId))
                queryParams.Add($"vessels={vesselId}");
            if (startDate.HasValue)
                queryParams.Add($"start-date={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"end-date={endDate.Value:yyyy-MM-dd}");

            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"v3/events?{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GfwEventsResponse>(_jsonOptions, cancellationToken);
            var events = result?.Entries?.Select(MapToEvent).ToList() ?? new List<GfwEvent>();

            // Cache the result
            await _cache.SetAsync(cacheKey, new GfwEventCollectionWrapper { Data = events }, CacheTtl, cancellationToken);

            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting port visits for vessel: {VesselId}", vesselId);
            return Enumerable.Empty<GfwEvent>();
        }
    }

    public async Task<IEnumerable<GfwEvent>> GetEncountersAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = CacheKeys.ForGfwEncounters(minLon, minLat, maxLon, maxLat, startDate, endDate);
        var cached = await _cache.GetAsync<GfwEventCollectionWrapper>(cacheKey, cancellationToken);
        if (cached?.Data != null)
        {
            _logger.LogDebug("GFW encounters cache hit for region");
            return cached.Data;
        }

        var events = await GetEventsAsync("encounter", minLon, minLat, maxLon, maxLat, startDate, endDate, limit, cancellationToken);
        var eventList = events.ToList();

        // Cache the result
        await _cache.SetAsync(cacheKey, new GfwEventCollectionWrapper { Data = eventList }, CacheTtl, cancellationToken);

        return eventList;
    }

    public async Task<GfwFishingEffortStats> GetFishingEffortStatsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = CacheKeys.ForGfwFishingStats(minLon, minLat, maxLon, maxLat, startDate, endDate);
        var cached = await _cache.GetAsync<GfwFishingStatsWrapper>(cacheKey, cancellationToken);
        if (cached?.Data != null)
        {
            _logger.LogDebug("GFW fishing stats cache hit for region");
            return cached.Data;
        }

        try
        {
            var geometry = $"{{\"type\":\"Polygon\",\"coordinates\":[[[{minLon},{minLat}],[{maxLon},{minLat}],[{maxLon},{maxLat}],[{minLon},{maxLat}],[{minLon},{minLat}]]]}}";
            var encodedGeometry = Uri.EscapeDataString(geometry);

            var response = await _httpClient.GetAsync(
                $"v3/4wings/stats?datasets=public-global-fishing-effort:latest&start-date={startDate:yyyy-MM-dd}&end-date={endDate:yyyy-MM-dd}&region={encodedGeometry}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GfwStatsResponse>(_jsonOptions, cancellationToken);
            var stats = new GfwFishingEffortStats
            {
                TotalFishingHours = result?.TotalFishingHours ?? 0,
                VesselCount = result?.VesselCount ?? 0,
                EventCount = result?.EventCount ?? 0,
                FishingHoursByFlag = result?.ByFlag ?? new Dictionary<string, double>(),
                FishingHoursByGearType = result?.ByGearType ?? new Dictionary<string, double>()
            };

            // Cache the result
            await _cache.SetAsync(cacheKey, new GfwFishingStatsWrapper { Data = stats }, CacheTtl, cancellationToken);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fishing effort stats");
            return new GfwFishingEffortStats();
        }
    }

    private async Task<IEnumerable<GfwEvent>> GetEventsAsync(
        string eventType,
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        DateTime startDate,
        DateTime endDate,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataset = eventType switch
            {
                "fishing" => "public-global-fishing-events:latest",
                "encounter" => "public-global-encounters-events:latest",
                "loitering" => "public-global-loitering-events:latest",
                _ => throw new ArgumentException($"Unknown event type: {eventType}")
            };

            var geometry = $"{{\"type\":\"Polygon\",\"coordinates\":[[[{minLon},{minLat}],[{maxLon},{minLat}],[{maxLon},{maxLat}],[{minLon},{maxLat}],[{minLon},{minLat}]]]}}";
            var encodedGeometry = Uri.EscapeDataString(geometry);

            var response = await _httpClient.GetAsync(
                $"v3/events?datasets={dataset}&start-date={startDate:yyyy-MM-dd}&end-date={endDate:yyyy-MM-dd}&region={encodedGeometry}&limit={limit}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GfwEventsResponse>(_jsonOptions, cancellationToken);
            return result?.Entries?.Select(MapToEvent) ?? Enumerable.Empty<GfwEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting {EventType} events", eventType);
            return Enumerable.Empty<GfwEvent>();
        }
    }

    private static GfwVesselInfo MapToVesselInfo(GfwVesselEntry entry)
    {
        return new GfwVesselInfo
        {
            VesselId = entry.Id ?? string.Empty,
            Name = entry.Shipname ?? entry.Name ?? "Unknown",
            Mmsi = entry.Mmsi,
            Imo = entry.Imo,
            CallSign = entry.Callsign,
            Flag = entry.Flag,
            VesselType = entry.VesselType,
            GearType = entry.GearType,
            LengthMeters = entry.Length,
            TonnageGt = entry.Tonnage,
            YearBuilt = entry.YearBuilt,
            LastPositionTime = entry.LastPositionTime
        };
    }

    private static GfwEvent MapToEvent(GfwEventEntry entry)
    {
        return new GfwEvent
        {
            EventId = entry.Id ?? string.Empty,
            EventType = entry.Type ?? "unknown",
            VesselId = entry.Vessel?.Id ?? string.Empty,
            VesselName = entry.Vessel?.Name,
            Longitude = entry.Position?.Lon ?? 0,
            Latitude = entry.Position?.Lat ?? 0,
            StartTime = entry.Start ?? DateTime.MinValue,
            EndTime = entry.End,
            DurationHours = entry.DurationHours,
            DistanceKm = entry.DistanceKm,
            PortName = entry.Port?.Name,
            EncounterVesselId = entry.Encounter?.Vessel?.Id
        };
    }

    private static string MapVesselType(VesselType type)
    {
        return type switch
        {
            VesselType.Fishing => "fishing",
            VesselType.Carrier => "carrier",
            VesselType.Support => "support",
            VesselType.Cargo => "cargo",
            VesselType.Tanker => "tanker",
            VesselType.Passenger => "passenger",
            _ => "other"
        };
    }

    // Internal DTOs for API response parsing
    private record GfwVesselSearchResponse
    {
        public List<GfwVesselEntry>? Entries { get; init; }
    }

    private record GfwVesselEntry
    {
        public string? Id { get; init; }
        public string? Shipname { get; init; }
        public string? Name { get; init; }
        public string? Mmsi { get; init; }
        public string? Imo { get; init; }
        public string? Callsign { get; init; }
        public string? Flag { get; init; }
        public string? VesselType { get; init; }
        public string? GearType { get; init; }
        public double? Length { get; init; }
        public double? Tonnage { get; init; }
        public int? YearBuilt { get; init; }
        public DateTime? LastPositionTime { get; init; }
    }

    private record GfwEventsResponse
    {
        public List<GfwEventEntry>? Entries { get; init; }
    }

    private record GfwEventEntry
    {
        public string? Id { get; init; }
        public string? Type { get; init; }
        public GfwVesselRef? Vessel { get; init; }
        public GfwPosition? Position { get; init; }
        public DateTime? Start { get; init; }
        public DateTime? End { get; init; }
        public double? DurationHours { get; init; }
        public double? DistanceKm { get; init; }
        public GfwPort? Port { get; init; }
        public GfwEncounter? Encounter { get; init; }
    }

    private record GfwVesselRef
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
    }

    private record GfwPosition
    {
        public double Lon { get; init; }
        public double Lat { get; init; }
    }

    private record GfwPort
    {
        public string? Name { get; init; }
    }

    private record GfwEncounter
    {
        public GfwVesselRef? Vessel { get; init; }
    }

    private record GfwStatsResponse
    {
        public double TotalFishingHours { get; init; }
        public int VesselCount { get; init; }
        public int EventCount { get; init; }
        public Dictionary<string, double>? ByFlag { get; init; }
        public Dictionary<string, double>? ByGearType { get; init; }
    }
}

/// <summary>
/// Configuration options for Global Fishing Watch API
/// </summary>
public class GlobalFishingWatchOptions
{
    public const string SectionName = "GlobalFishingWatch";

    /// <summary>
    /// API token for authentication
    /// Get one at: https://globalfishingwatch.org/our-apis/tokens
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable the API client (false for development without API key)
    /// </summary>
    public bool Enabled { get; set; } = true;
}

#region Cache Wrapper Classes

/// <summary>
/// Wrapper for caching GFW vessel info
/// </summary>
internal class GfwVesselWrapper
{
    public GfwVesselInfo? Data { get; set; }
}

/// <summary>
/// Wrapper for caching GFW vessel collections
/// </summary>
internal class GfwVesselCollectionWrapper
{
    public List<GfwVesselInfo>? Data { get; set; }
}

/// <summary>
/// Wrapper for caching GFW event collections
/// </summary>
internal class GfwEventCollectionWrapper
{
    public List<GfwEvent>? Data { get; set; }
}

/// <summary>
/// Wrapper for caching GFW fishing effort stats
/// </summary>
internal class GfwFishingStatsWrapper
{
    public GfwFishingEffortStats? Data { get; set; }
}

#endregion
