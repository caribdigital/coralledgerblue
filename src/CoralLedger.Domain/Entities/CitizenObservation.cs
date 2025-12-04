using CoralLedger.Domain.Common;
using CoralLedger.Domain.Enums;
using NetTopologySuite.Geometries;

namespace CoralLedger.Domain.Entities;

public class CitizenObservation : BaseEntity, IAuditableEntity
{
    public Point Location { get; private set; } = null!;
    public DateTime ObservationTime { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ObservationType Type { get; private set; }
    public int Severity { get; private set; } = 3; // 1-5 scale, 3 = moderate

    // Citizen attribution (no authentication required)
    public string? CitizenEmail { get; private set; }
    public string? CitizenName { get; private set; }

    // Spatial context
    public bool? IsInMpa { get; private set; }
    public Guid? MarineProtectedAreaId { get; private set; }
    public MarineProtectedArea? MarineProtectedArea { get; private set; }
    public Guid? ReefId { get; private set; }
    public Reef? Reef { get; private set; }

    // Moderation
    public ObservationStatus Status { get; private set; } = ObservationStatus.Pending;
    public string? ModerationNotes { get; private set; }
    public DateTime? ModeratedAt { get; private set; }

    // Photos
    public ICollection<ObservationPhoto> Photos { get; private set; } = new List<ObservationPhoto>();

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private CitizenObservation() { }

    public static CitizenObservation Create(
        Point location,
        DateTime observationTime,
        string title,
        ObservationType type,
        string? description = null,
        int severity = 3,
        string? citizenEmail = null,
        string? citizenName = null)
    {
        if (severity < 1 || severity > 5)
            throw new ArgumentOutOfRangeException(nameof(severity), "Severity must be between 1 and 5");

        return new CitizenObservation
        {
            Id = Guid.NewGuid(),
            Location = location,
            ObservationTime = observationTime,
            Title = title,
            Description = description,
            Type = type,
            Severity = severity,
            CitizenEmail = citizenEmail,
            CitizenName = citizenName,
            Status = ObservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetMpaContext(bool isInMpa, Guid? mpaId = null, Guid? reefId = null)
    {
        IsInMpa = isInMpa;
        MarineProtectedAreaId = mpaId;
        ReefId = reefId;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Approve(string? notes = null)
    {
        Status = ObservationStatus.Approved;
        ModerationNotes = notes;
        ModeratedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        Status = ObservationStatus.Rejected;
        ModerationNotes = reason;
        ModeratedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void RequestReview(string reason)
    {
        Status = ObservationStatus.NeedsReview;
        ModerationNotes = reason;
        ModifiedAt = DateTime.UtcNow;
    }

    public void AddPhoto(ObservationPhoto photo)
    {
        Photos.Add(photo);
        ModifiedAt = DateTime.UtcNow;
    }
}
