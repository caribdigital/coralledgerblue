namespace CoralLedger.Application.Features.MarineProtectedAreas.DTOs;

public record MpaSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double AreaSquareKm { get; init; }
    public string ProtectionLevel { get; init; } = string.Empty;
    public string IslandGroup { get; init; } = string.Empty;
    public double CentroidLongitude { get; init; }
    public double CentroidLatitude { get; init; }
}
