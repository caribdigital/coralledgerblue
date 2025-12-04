using CoralLedger.Domain.Common;

namespace CoralLedger.Domain.Entities;

/// <summary>
/// Links a citizen observation to identified species
/// Supports both AI classification and manual identification
/// </summary>
public class SpeciesObservation : BaseEntity
{
    public Guid CitizenObservationId { get; private set; }
    public CitizenObservation CitizenObservation { get; private set; } = null!;

    public Guid BahamianSpeciesId { get; private set; }
    public BahamianSpecies BahamianSpecies { get; private set; } = null!;

    public int? Quantity { get; private set; }
    public double? AiConfidenceScore { get; private set; }
    public bool RequiresExpertVerification { get; private set; }
    public bool IsAiGenerated { get; private set; }
    public string? Notes { get; private set; }
    public DateTime IdentifiedAt { get; private set; }

    private SpeciesObservation() { }

    public static SpeciesObservation Create(
        Guid citizenObservationId,
        Guid bahamianSpeciesId,
        int? quantity = null,
        double? aiConfidenceScore = null,
        bool isAiGenerated = false,
        string? notes = null)
    {
        var observation = new SpeciesObservation
        {
            CitizenObservationId = citizenObservationId,
            BahamianSpeciesId = bahamianSpeciesId,
            Quantity = quantity,
            AiConfidenceScore = aiConfidenceScore,
            IsAiGenerated = isAiGenerated,
            RequiresExpertVerification = aiConfidenceScore.HasValue && aiConfidenceScore.Value < 85,
            Notes = notes,
            IdentifiedAt = DateTime.UtcNow
        };

        return observation;
    }

    public void MarkAsVerified()
    {
        RequiresExpertVerification = false;
    }

    public void UpdateConfidence(double newScore)
    {
        AiConfidenceScore = newScore;
        RequiresExpertVerification = newScore < 85;
    }
}
