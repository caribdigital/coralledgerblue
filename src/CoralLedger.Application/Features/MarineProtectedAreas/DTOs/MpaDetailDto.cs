namespace CoralLedger.Application.Features.MarineProtectedAreas.DTOs;

public record MpaDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? LocalName { get; init; }
    public string? WdpaId { get; init; }
    public double AreaSquareKm { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ProtectionLevel { get; init; } = string.Empty;
    public string IslandGroup { get; init; } = string.Empty;
    public DateOnly? DesignationDate { get; init; }
    public string? ManagingAuthority { get; init; }
    public string? Description { get; init; }
    public double CentroidLongitude { get; init; }
    public double CentroidLatitude { get; init; }
    public string? BoundaryGeoJson { get; init; }
    public int ReefCount { get; init; }
}
