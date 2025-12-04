using CoralLedger.Domain.Common;
using CoralLedger.Domain.Enums;

namespace CoralLedger.Domain.Entities;

/// <summary>
/// Bahamian marine species database entry
/// Includes scientific name, common name, local Bahamian name, and conservation status
/// </summary>
public class BahamianSpecies : BaseEntity
{
    public string ScientificName { get; private set; } = string.Empty;
    public string CommonName { get; private set; } = string.Empty;
    public string? LocalName { get; private set; }
    public SpeciesCategory Category { get; private set; }
    public ConservationStatus ConservationStatus { get; private set; }
    public bool IsInvasive { get; private set; }
    public string? Description { get; private set; }
    public string? IdentificationTips { get; private set; }
    public string? Habitat { get; private set; }
    public int? TypicalDepthMinM { get; private set; }
    public int? TypicalDepthMaxM { get; private set; }

    public ICollection<SpeciesObservation> Observations { get; private set; } = new List<SpeciesObservation>();

    private BahamianSpecies() { }

    public static BahamianSpecies Create(
        string scientificName,
        string commonName,
        SpeciesCategory category,
        ConservationStatus conservationStatus,
        string? localName = null,
        bool isInvasive = false,
        string? description = null,
        string? identificationTips = null,
        string? habitat = null,
        int? typicalDepthMinM = null,
        int? typicalDepthMaxM = null)
    {
        if (string.IsNullOrWhiteSpace(scientificName))
            throw new ArgumentException("Scientific name is required", nameof(scientificName));
        if (string.IsNullOrWhiteSpace(commonName))
            throw new ArgumentException("Common name is required", nameof(commonName));

        return new BahamianSpecies
        {
            ScientificName = scientificName,
            CommonName = commonName,
            LocalName = localName,
            Category = category,
            ConservationStatus = conservationStatus,
            IsInvasive = isInvasive,
            Description = description,
            IdentificationTips = identificationTips,
            Habitat = habitat,
            TypicalDepthMinM = typicalDepthMinM,
            TypicalDepthMaxM = typicalDepthMaxM
        };
    }

    public bool IsThreatened => ConservationStatus >= ConservationStatus.Vulnerable;

    public bool RequiresPriorityAlert => IsInvasive || ConservationStatus >= ConservationStatus.Endangered;
}
