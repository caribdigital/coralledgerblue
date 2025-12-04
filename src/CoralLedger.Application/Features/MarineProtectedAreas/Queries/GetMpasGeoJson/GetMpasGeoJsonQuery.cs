using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetMpasGeoJson;

/// <summary>
/// Query to get all MPAs as GeoJSON FeatureCollection with configurable geometry resolution
/// </summary>
/// <param name="Resolution">Geometry resolution level (default: Medium for map performance)</param>
public record GetMpasGeoJsonQuery(GeometryResolution Resolution = GeometryResolution.Medium) : IRequest<MpaGeoJsonCollection>;

/// <summary>
/// GeoJSON FeatureCollection - property names must be lowercase per GeoJSON spec
/// </summary>
public class MpaGeoJsonCollection
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "FeatureCollection";

    [JsonPropertyName("features")]
    public List<MpaGeoJsonFeature> Features { get; init; } = new();
}

/// <summary>
/// GeoJSON Feature - property names must be lowercase per GeoJSON spec
/// </summary>
public class MpaGeoJsonFeature
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Feature";

    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("geometry")]
    public JsonNode? Geometry { get; init; }

    [JsonPropertyName("properties")]
    public MpaGeoJsonProperties Properties { get; init; } = new();
}

/// <summary>
/// GeoJSON Feature properties - use PascalCase for app-specific properties
/// </summary>
public class MpaGeoJsonProperties
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("ProtectionLevel")]
    public string ProtectionLevel { get; init; } = "";

    [JsonPropertyName("IslandGroup")]
    public string IslandGroup { get; init; } = "";

    [JsonPropertyName("AreaSquareKm")]
    public double AreaSquareKm { get; init; }

    [JsonPropertyName("Status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("CentroidLongitude")]
    public double CentroidLongitude { get; init; }

    [JsonPropertyName("CentroidLatitude")]
    public double CentroidLatitude { get; init; }
}

public class GetMpasGeoJsonQueryHandler : IRequestHandler<GetMpasGeoJsonQuery, MpaGeoJsonCollection>
{
    private readonly IMarineDbContext _context;

    public GetMpasGeoJsonQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<MpaGeoJsonCollection> Handle(
        GetMpasGeoJsonQuery request,
        CancellationToken cancellationToken)
    {
        var mpas = await _context.MarineProtectedAreas
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var geoJsonWriter = new GeoJsonWriter();
        var features = new List<MpaGeoJsonFeature>();

        foreach (var mpa in mpas)
        {
            // Select appropriate geometry based on requested resolution
            var geometry = request.Resolution switch
            {
                GeometryResolution.Full => mpa.Boundary,
                GeometryResolution.Low => mpa.BoundarySimplifiedLow ?? mpa.Boundary,
                _ => mpa.BoundarySimplifiedMedium ?? mpa.Boundary // Medium is default
            };

            var geometryJson = geoJsonWriter.Write(geometry);
            var geometryNode = JsonNode.Parse(geometryJson);

            features.Add(new MpaGeoJsonFeature
            {
                Id = mpa.Id.ToString(),
                Geometry = geometryNode,
                Properties = new MpaGeoJsonProperties
                {
                    Name = mpa.Name,
                    ProtectionLevel = mpa.ProtectionLevel.ToString(),
                    IslandGroup = mpa.IslandGroup.ToString(),
                    AreaSquareKm = mpa.AreaSquareKm,
                    Status = mpa.Status.ToString(),
                    CentroidLongitude = mpa.Centroid.X,
                    CentroidLatitude = mpa.Centroid.Y
                }
            });
        }

        return new MpaGeoJsonCollection { Features = features };
    }
}
