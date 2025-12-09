using CoralLedger.Domain.Common;
using CoralLedger.Domain.Enums;

namespace CoralLedger.Domain.Entities;

/// <summary>
/// Report of AI species misidentification for feedback loop
/// Sprint 4.3 US-4.3.4: 'Report Misidentification' to improve the model
/// </summary>
public class SpeciesMisidentificationReport : BaseEntity
{
    public Guid SpeciesObservationId { get; private set; }
    public SpeciesObservation SpeciesObservation { get; private set; } = null!;

    /// <summary>
    /// The scientific name that was incorrectly identified by AI
    /// </summary>
    public string IncorrectScientificName { get; private set; } = string.Empty;

    /// <summary>
    /// The correct scientific name provided by the reporter
    /// </summary>
    public string? CorrectedScientificName { get; private set; }

    /// <summary>
    /// Reference to the correct species if in database
    /// </summary>
    public Guid? CorrectedSpeciesId { get; private set; }
    public BahamianSpecies? CorrectedSpecies { get; private set; }

    /// <summary>
    /// Reporter's explanation of why the identification is incorrect
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Email of the person reporting (optional)
    /// </summary>
    public string? ReporterEmail { get; private set; }

    /// <summary>
    /// Name of the person reporting (optional)
    /// </summary>
    public string? ReporterName { get; private set; }

    /// <summary>
    /// Reporter's expertise level
    /// </summary>
    public ReporterExpertise Expertise { get; private set; }

    /// <summary>
    /// Current review status
    /// </summary>
    public MisidentificationReportStatus Status { get; private set; }

    /// <summary>
    /// When the report was created
    /// </summary>
    public DateTime ReportedAt { get; private set; }

    /// <summary>
    /// When the report was reviewed (if applicable)
    /// </summary>
    public DateTime? ReviewedAt { get; private set; }

    /// <summary>
    /// Reviewer notes
    /// </summary>
    public string? ReviewNotes { get; private set; }

    private SpeciesMisidentificationReport() { }

    public static SpeciesMisidentificationReport Create(
        Guid speciesObservationId,
        string incorrectScientificName,
        string reason,
        string? correctedScientificName = null,
        Guid? correctedSpeciesId = null,
        string? reporterEmail = null,
        string? reporterName = null,
        ReporterExpertise expertise = ReporterExpertise.CitizenScientist)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason for misidentification report is required", nameof(reason));

        return new SpeciesMisidentificationReport
        {
            SpeciesObservationId = speciesObservationId,
            IncorrectScientificName = incorrectScientificName,
            CorrectedScientificName = correctedScientificName,
            CorrectedSpeciesId = correctedSpeciesId,
            Reason = reason,
            ReporterEmail = reporterEmail,
            ReporterName = reporterName,
            Expertise = expertise,
            Status = MisidentificationReportStatus.Pending,
            ReportedAt = DateTime.UtcNow
        };
    }

    public void MarkAsReviewed(MisidentificationReportStatus newStatus, string? notes = null)
    {
        Status = newStatus;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
    }

    public void UpdateCorrectedSpecies(Guid speciesId, string scientificName)
    {
        CorrectedSpeciesId = speciesId;
        CorrectedScientificName = scientificName;
    }
}

/// <summary>
/// Reporter's self-reported expertise level
/// </summary>
public enum ReporterExpertise
{
    Unknown = 0,
    CitizenScientist = 1,
    DiveInstructor = 2,
    MarineBiologist = 3,
    TaxonomyExpert = 4
}

/// <summary>
/// Status of misidentification report review
/// </summary>
public enum MisidentificationReportStatus
{
    Pending = 0,
    UnderReview = 1,
    Confirmed = 2,      // Misidentification confirmed
    Rejected = 3,       // Original identification was correct
    Inconclusive = 4    // Cannot determine
}
