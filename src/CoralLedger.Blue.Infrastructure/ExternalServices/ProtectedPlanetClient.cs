using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace CoralLedger.Blue.Infrastructure.ExternalServices;

/// <summary>
/// Client implementation for Protected Planet WDPA API v4
/// https://api.protectedplanet.net/documentation
/// </summary>
public class ProtectedPlanetClient : IProtectedPlanetClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProtectedPlanetClient> _logger;
    private readonly ProtectedPlanetOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly GeoJsonReader _geoJsonReader;

    public ProtectedPlanetClient(
        HttpClient httpClient,
        IOptions<ProtectedPlanetOptions> options,
        ILogger<ProtectedPlanetClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _geoJsonReader = new GeoJsonReader();

        // Warn if enabled but missing token
        if (_options.Enabled && string.IsNullOrEmpty(_options.ApiToken))
        {
            _logger.LogWarning(
                "ProtectedPlanet is enabled but ApiToken is not configured. " +
                "API calls will fail. Request a token at https://api.protectedplanet.net/request " +
                "and set it using: " +
                "dotnet user-secrets set \"ProtectedPlanet:ApiToken\" \"your-token\" --project src/CoralLedger.Blue.Web");
        }
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrEmpty(_options.ApiToken);

    /// <inheritdoc />
    public async Task<ProtectedAreaDto?> GetProtectedAreaAsync(
        string wdpaId,
        bool withGeometry = true,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Protected Planet API token not configured. Cannot fetch WDPA ID: {WdpaId}", wdpaId);
            return null;
        }

        try
        {
            var url = $"protected_areas/{wdpaId}?token={_options.ApiToken}&with_geometry={withGeometry.ToString().ToLower()}";

            _logger.LogDebug("Fetching protected area: {WdpaId}", wdpaId);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch protected area {WdpaId}. Status: {StatusCode}",
                    wdpaId, response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ProtectedPlanetSingleResponse>(_jsonOptions, cancellationToken);

            if (result?.ProtectedArea == null)
            {
                _logger.LogWarning("Protected area not found: {WdpaId}", wdpaId);
                return null;
            }

            return MapToDto(result.ProtectedArea);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching protected area: {WdpaId}", wdpaId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProtectedAreaSearchResult> SearchByCountryAsync(
        string iso3Code,
        bool withGeometry = false,
        bool marineOnly = true,
        int page = 1,
        int perPage = 50,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Protected Planet API token not configured. Cannot search country: {Country}", iso3Code);
            return new ProtectedAreaSearchResult();
        }

        try
        {
            var marine = marineOnly ? "&marine=true" : "";
            var url = $"protected_areas/search?token={_options.ApiToken}" +
                      $"&country={iso3Code}" +
                      $"&with_geometry={withGeometry.ToString().ToLower()}" +
                      $"&page={page}&per_page={perPage}{marine}";

            _logger.LogDebug("Searching protected areas for country: {Country}", iso3Code);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ProtectedPlanetSearchResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                return new ProtectedAreaSearchResult();
            }

            var areas = result.ProtectedAreas?
                .Select(MapToDto)
                .ToList() ?? [];

            return new ProtectedAreaSearchResult
            {
                ProtectedAreas = areas,
                CurrentPage = page,
                TotalPages = result.Pagination?.TotalPages ?? 1,
                TotalCount = result.Pagination?.TotalEntries ?? areas.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching protected areas for country: {Country}", iso3Code);
            return new ProtectedAreaSearchResult();
        }
    }

    private ProtectedAreaDto MapToDto(ProtectedPlanetArea area)
    {
        Geometry? boundary = null;

        if (area.Geojson?.Geometry != null)
        {
            try
            {
                var geoJsonString = JsonSerializer.Serialize(area.Geojson.Geometry, _jsonOptions);
                boundary = _geoJsonReader.Read<Geometry>(geoJsonString);

                // Ensure SRID is set to 4326 (WGS84)
                if (boundary != null)
                {
                    boundary.SRID = 4326;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse geometry for WDPA ID: {SiteId}", area.Id);
            }
        }

        return new ProtectedAreaDto
        {
            SiteId = area.Id?.ToString() ?? string.Empty,
            Name = area.Name ?? string.Empty,
            NameEnglish = area.EnglishName,
            OriginalName = area.OriginalName,
            ReportedArea = area.ReportedArea,
            GisArea = area.GisArea,
            GisMarineArea = area.GisMarineArea,
            Designation = area.Designation?.Name,
            IucnCategory = area.IucnCategory?.Name,
            Governance = area.Governance?.Name,
            ManagementAuthority = area.ManagementAuthority?.Name,
            DesignationYear = area.LegalStatusUpdatedAt?.Year,
            IsMarine = area.Marine ?? false,
            Status = area.LegalStatus?.Name,
            CountryIso3 = area.Countries?.FirstOrDefault()?.Iso3,
            Boundary = boundary
        };
    }

    #region Internal DTOs for API Response Parsing

    private record ProtectedPlanetSingleResponse
    {
        [JsonPropertyName("protected_area")]
        public ProtectedPlanetArea? ProtectedArea { get; init; }
    }

    private record ProtectedPlanetSearchResponse
    {
        [JsonPropertyName("protected_areas")]
        public List<ProtectedPlanetArea>? ProtectedAreas { get; init; }

        public PaginationInfo? Pagination { get; init; }
    }

    private record PaginationInfo
    {
        [JsonPropertyName("current_page")]
        public int CurrentPage { get; init; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; init; }

        [JsonPropertyName("total_entries")]
        public int TotalEntries { get; init; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; init; }
    }

    private record ProtectedPlanetArea
    {
        public int? Id { get; init; }
        public string? Name { get; init; }

        [JsonPropertyName("english_name")]
        public string? EnglishName { get; init; }

        [JsonPropertyName("original_name")]
        public string? OriginalName { get; init; }

        [JsonPropertyName("reported_area")]
        public double? ReportedArea { get; init; }

        [JsonPropertyName("gis_area")]
        public double? GisArea { get; init; }

        [JsonPropertyName("gis_marine_area")]
        public double? GisMarineArea { get; init; }

        public bool? Marine { get; init; }

        [JsonPropertyName("legal_status")]
        public NamedItem? LegalStatus { get; init; }

        [JsonPropertyName("legal_status_updated_at")]
        public DateTime? LegalStatusUpdatedAt { get; init; }

        public NamedItem? Designation { get; init; }

        [JsonPropertyName("iucn_category")]
        public NamedItem? IucnCategory { get; init; }

        public NamedItem? Governance { get; init; }

        [JsonPropertyName("management_authority")]
        public NamedItem? ManagementAuthority { get; init; }

        public List<CountryInfo>? Countries { get; init; }

        public GeoJsonWrapper? Geojson { get; init; }
    }

    private record NamedItem
    {
        public int? Id { get; init; }
        public string? Name { get; init; }
    }

    private record CountryInfo
    {
        public string? Name { get; init; }
        public string? Iso3 { get; init; }
    }

    private record GeoJsonWrapper
    {
        public string? Type { get; init; }
        public JsonElement? Geometry { get; init; }
    }

    #endregion
}
