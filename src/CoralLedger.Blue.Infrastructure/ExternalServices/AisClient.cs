using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoralLedger.Blue.Infrastructure.ExternalServices;

/// <summary>
/// AIS client supporting multiple providers (MarineTraffic, AISHub, etc.)
/// </summary>
public class AisClient : IAisClient
{
    private readonly HttpClient _httpClient;
    private readonly AisOptions _options;
    private readonly ILogger<AisClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AisClient(
        HttpClient httpClient,
        IOptions<AisOptions> options,
        ILogger<AisClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public bool IsConfigured => _options.Enabled && !string.IsNullOrEmpty(_options.ApiKey);

    public async Task<ServiceResult<IReadOnlyList<AisVesselPosition>>> GetVesselPositionsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("AIS client is not configured, returning demo data");
            return ServiceResult<IReadOnlyList<AisVesselPosition>>.Ok(GetDemoVesselPositions());
        }

        try
        {
            var bbox = _options.BoundingBox;

            // MarineTraffic API format
            var url = _options.Provider switch
            {
                "MarineTraffic" => $"exportvessel/v:8/{_options.ApiKey}/MINLAT:{bbox.MinLat}/MAXLAT:{bbox.MaxLat}/MINLON:{bbox.MinLon}/MAXLON:{bbox.MaxLon}/protocol:jsono",
                "AISHub" => $"?username={_options.ApiKey}&format=1&output=json&compress=0&latmin={bbox.MinLat}&latmax={bbox.MaxLat}&lonmin={bbox.MinLon}&lonmax={bbox.MaxLon}",
                _ => throw new NotSupportedException($"AIS provider {_options.Provider} is not supported")
            };

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var positions = _options.Provider switch
            {
                "MarineTraffic" => ParseMarineTrafficResponse(content),
                "AISHub" => ParseAisHubResponse(content),
                _ => Array.Empty<AisVesselPosition>()
            };

            return ServiceResult<IReadOnlyList<AisVesselPosition>>.Ok(positions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching AIS vessel positions");
            return ServiceResult<IReadOnlyList<AisVesselPosition>>.Fail(
                "Failed to fetch AIS vessel positions from provider", ex);
        }
    }

    public async Task<ServiceResult<IReadOnlyList<AisVesselPosition>>> GetVesselPositionsNearAsync(
        double longitude,
        double latitude,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        // Get all positions and filter by distance
        var result = await GetVesselPositionsAsync(cancellationToken);

        if (!result.Success)
        {
            return result;
        }

        var nearbyPositions = result.Value!.Where(p =>
            CalculateDistanceKm(longitude, latitude, p.Longitude, p.Latitude) <= radiusKm
        ).ToList();

        return ServiceResult<IReadOnlyList<AisVesselPosition>>.Ok(nearbyPositions);
    }

    public async Task<ServiceResult<IReadOnlyList<AisVesselPosition>>> GetVesselTrackAsync(
        string mmsi,
        int hours = 24,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            // Return empty track when not configured (demo mode doesn't support track history)
            _logger.LogWarning("AIS client is not configured, track data unavailable");
            return ServiceResult<IReadOnlyList<AisVesselPosition>>.OkEmpty();
        }

        try
        {
            // MarineTraffic track API
            if (_options.Provider == "MarineTraffic")
            {
                var url = $"exportvesseltrack/v:2/{_options.ApiKey}/mmsi:{mmsi}/days:1/protocol:jsono";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var track = ParseMarineTrafficTrackResponse(content, mmsi);
                return ServiceResult<IReadOnlyList<AisVesselPosition>>.Ok(track);
            }

            // Provider doesn't support track data
            return ServiceResult<IReadOnlyList<AisVesselPosition>>.OkEmpty();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vessel track for MMSI {Mmsi}", mmsi);
            return ServiceResult<IReadOnlyList<AisVesselPosition>>.Fail(
                $"Failed to fetch vessel track for MMSI {mmsi}", ex);
        }
    }

    private IReadOnlyList<AisVesselPosition> ParseMarineTrafficResponse(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<List<MarineTrafficVessel>>(json, JsonOptions);
            return data?.Select(v => new AisVesselPosition
            {
                Mmsi = v.Mmsi ?? "",
                Imo = v.Imo,
                Name = v.ShipName ?? "Unknown",
                CallSign = v.CallSign,
                Longitude = v.Lon ?? 0,
                Latitude = v.Lat ?? 0,
                Speed = v.Speed,
                Course = v.Course,
                Heading = v.Heading,
                Destination = v.Destination,
                VesselType = v.ShipType,
                Flag = v.Flag,
                Length = v.Length,
                Width = v.Width,
                Timestamp = v.Timestamp ?? DateTime.UtcNow,
                NavigationStatus = v.Status
            }).ToList() ?? new List<AisVesselPosition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MarineTraffic response");
            return Array.Empty<AisVesselPosition>();
        }
    }

    private IReadOnlyList<AisVesselPosition> ParseMarineTrafficTrackResponse(string json, string mmsi)
    {
        try
        {
            var data = JsonSerializer.Deserialize<List<MarineTrafficTrackPoint>>(json, JsonOptions);
            return data?.Select(p => new AisVesselPosition
            {
                Mmsi = mmsi,
                Name = "Track Point",
                Longitude = p.Lon ?? 0,
                Latitude = p.Lat ?? 0,
                Speed = p.Speed,
                Course = p.Course,
                Timestamp = p.Timestamp ?? DateTime.UtcNow
            }).ToList() ?? new List<AisVesselPosition>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse MarineTraffic track response for MMSI {Mmsi}", mmsi);
            return Array.Empty<AisVesselPosition>();
        }
    }

    private IReadOnlyList<AisVesselPosition> ParseAisHubResponse(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<List<AisHubVessel>>(json, JsonOptions);
            return data?.Select(v => new AisVesselPosition
            {
                Mmsi = v.Mmsi?.ToString() ?? "",
                Imo = v.Imo?.ToString(),
                Name = v.Name ?? "Unknown",
                CallSign = v.Callsign,
                Longitude = v.Longitude ?? 0,
                Latitude = v.Latitude ?? 0,
                Speed = v.Sog,
                Course = v.Cog,
                Heading = v.Heading,
                Destination = v.Destination,
                VesselType = v.Type?.ToString(),
                Timestamp = DateTime.UtcNow
            }).ToList() ?? new List<AisVesselPosition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing AISHub response");
            return Array.Empty<AisVesselPosition>();
        }
    }

    /// <summary>
    /// Returns demo vessel positions for testing when API is not configured
    /// </summary>
    private IReadOnlyList<AisVesselPosition> GetDemoVesselPositions()
    {
        var random = new Random();
        var now = DateTime.UtcNow;

        // Demo vessels around Bahamas waters
        return new List<AisVesselPosition>
        {
            new()
            {
                Mmsi = "311000001",
                Name = "BAHAMAS EXPLORER",
                Longitude = -77.35 + (random.NextDouble() - 0.5) * 0.1,
                Latitude = 25.05 + (random.NextDouble() - 0.5) * 0.1,
                Speed = 8.5,
                Course = 45,
                VesselType = "Fishing",
                Flag = "BS",
                Timestamp = now.AddMinutes(-random.Next(1, 10))
            },
            new()
            {
                Mmsi = "311000002",
                Name = "NASSAU PEARL",
                Longitude = -77.78 + (random.NextDouble() - 0.5) * 0.1,
                Latitude = 24.12 + (random.NextDouble() - 0.5) * 0.1,
                Speed = 12.3,
                Course = 180,
                VesselType = "Cargo",
                Flag = "BS",
                Timestamp = now.AddMinutes(-random.Next(1, 10))
            },
            new()
            {
                Mmsi = "311000003",
                Name = "EXUMA DIVER",
                Longitude = -76.52 + (random.NextDouble() - 0.5) * 0.1,
                Latitude = 23.82 + (random.NextDouble() - 0.5) * 0.1,
                Speed = 5.2,
                Course = 270,
                VesselType = "Pleasure",
                Flag = "BS",
                Timestamp = now.AddMinutes(-random.Next(1, 10))
            },
            new()
            {
                Mmsi = "311000004",
                Name = "ANDROS FISHER",
                Longitude = -78.15 + (random.NextDouble() - 0.5) * 0.1,
                Latitude = 24.45 + (random.NextDouble() - 0.5) * 0.1,
                Speed = 3.1,
                Course = 90,
                VesselType = "Fishing",
                Flag = "BS",
                Timestamp = now.AddMinutes(-random.Next(1, 10))
            },
            new()
            {
                Mmsi = "311000005",
                Name = "ABACO STAR",
                Longitude = -77.08 + (random.NextDouble() - 0.5) * 0.1,
                Latitude = 26.55 + (random.NextDouble() - 0.5) * 0.1,
                Speed = 15.7,
                Course = 315,
                VesselType = "Tanker",
                Flag = "PA",
                Timestamp = now.AddMinutes(-random.Next(1, 10))
            }
        };
    }

    private static double CalculateDistanceKm(double lon1, double lat1, double lon2, double lat2)
    {
        const double R = 6371; // Earth's radius in km
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}

// MarineTraffic API response models
internal class MarineTrafficVessel
{
    [JsonPropertyName("MMSI")]
    public string? Mmsi { get; set; }

    [JsonPropertyName("IMO")]
    public string? Imo { get; set; }

    [JsonPropertyName("SHIPNAME")]
    public string? ShipName { get; set; }

    [JsonPropertyName("CALLSIGN")]
    public string? CallSign { get; set; }

    [JsonPropertyName("LON")]
    public double? Lon { get; set; }

    [JsonPropertyName("LAT")]
    public double? Lat { get; set; }

    [JsonPropertyName("SPEED")]
    public double? Speed { get; set; }

    [JsonPropertyName("COURSE")]
    public double? Course { get; set; }

    [JsonPropertyName("HEADING")]
    public double? Heading { get; set; }

    [JsonPropertyName("DESTINATION")]
    public string? Destination { get; set; }

    [JsonPropertyName("SHIPTYPE")]
    public string? ShipType { get; set; }

    [JsonPropertyName("FLAG")]
    public string? Flag { get; set; }

    [JsonPropertyName("LENGTH")]
    public double? Length { get; set; }

    [JsonPropertyName("WIDTH")]
    public double? Width { get; set; }

    [JsonPropertyName("TIMESTAMP")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("STATUS")]
    public string? Status { get; set; }
}

internal class MarineTrafficTrackPoint
{
    [JsonPropertyName("LON")]
    public double? Lon { get; set; }

    [JsonPropertyName("LAT")]
    public double? Lat { get; set; }

    [JsonPropertyName("SPEED")]
    public double? Speed { get; set; }

    [JsonPropertyName("COURSE")]
    public double? Course { get; set; }

    [JsonPropertyName("TIMESTAMP")]
    public DateTime? Timestamp { get; set; }
}

internal class AisHubVessel
{
    public long? Mmsi { get; set; }
    public long? Imo { get; set; }
    public string? Name { get; set; }
    public string? Callsign { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public double? Sog { get; set; }
    public double? Cog { get; set; }
    public double? Heading { get; set; }
    public string? Destination { get; set; }
    public int? Type { get; set; }
}
