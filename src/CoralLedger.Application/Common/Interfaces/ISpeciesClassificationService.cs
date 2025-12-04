namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// AI service for classifying marine species from photos
/// </summary>
public interface ISpeciesClassificationService
{
    /// <summary>
    /// Whether the classification service is configured and available
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Classify marine species in a photo
    /// </summary>
    Task<SpeciesClassificationResult> ClassifyPhotoAsync(
        string photoUri,
        CancellationToken cancellationToken = default);
}

public record SpeciesClassificationResult(
    bool Success,
    IReadOnlyList<IdentifiedSpecies> Species,
    string? Error = null);

public record IdentifiedSpecies(
    string ScientificName,
    string CommonName,
    double ConfidenceScore,
    bool RequiresExpertVerification,
    bool IsInvasive,
    bool IsConservationConcern,
    string? HealthStatus,
    string? Notes);
