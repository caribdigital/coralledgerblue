using CoralLedger.Blue.Domain.Common;
using CoralLedger.Blue.Domain.Enums;

namespace CoralLedger.Blue.Domain.Entities;

public class UserProfile : BaseEntity, IAuditableEntity
{
    public string CitizenEmail { get; private set; } = string.Empty;
    public string? CitizenName { get; private set; }
    public ObserverTier Tier { get; private set; } = ObserverTier.None;
    public int TotalObservations { get; private set; }
    public int VerifiedObservations { get; private set; }
    public int RejectedObservations { get; private set; }
    public double AccuracyRate { get; private set; }
    public DateTime? LastObservationAt { get; private set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private UserProfile() { }

    public static UserProfile Create(string citizenEmail, string? citizenName = null)
    {
        if (string.IsNullOrWhiteSpace(citizenEmail))
            throw new ArgumentException("Citizen email is required", nameof(citizenEmail));

        return new UserProfile
        {
            Id = Guid.NewGuid(),
            CitizenEmail = citizenEmail,
            CitizenName = citizenName,
            Tier = ObserverTier.None,
            TotalObservations = 0,
            VerifiedObservations = 0,
            RejectedObservations = 0,
            AccuracyRate = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void IncrementObservations()
    {
        TotalObservations++;
        LastObservationAt = DateTime.UtcNow;
        UpdateAccuracyRate();
        UpdateTier();
        ModifiedAt = DateTime.UtcNow;
    }

    public void RecordVerifiedObservation()
    {
        VerifiedObservations++;
        UpdateAccuracyRate();
        UpdateTier();
        ModifiedAt = DateTime.UtcNow;
    }

    public void RecordRejectedObservation()
    {
        RejectedObservations++;
        UpdateAccuracyRate();
        UpdateTier();
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateName(string name)
    {
        CitizenName = name;
        ModifiedAt = DateTime.UtcNow;
    }

    private void UpdateAccuracyRate()
    {
        var reviewed = VerifiedObservations + RejectedObservations;
        if (reviewed == 0)
        {
            AccuracyRate = 0;
            return;
        }

        AccuracyRate = (double)VerifiedObservations / reviewed * 100;
    }

    private void UpdateTier()
    {
        // Tier requirements:
        // Bronze: 10+ verified observations, 70%+ accuracy
        // Silver: 50+ verified observations, 80%+ accuracy
        // Gold: 100+ verified observations, 90%+ accuracy
        
        if (VerifiedObservations >= 100 && AccuracyRate >= 90)
        {
            Tier = ObserverTier.Gold;
        }
        else if (VerifiedObservations >= 50 && AccuracyRate >= 80)
        {
            Tier = ObserverTier.Silver;
        }
        else if (VerifiedObservations >= 10 && AccuracyRate >= 70)
        {
            Tier = ObserverTier.Bronze;
        }
        else
        {
            Tier = ObserverTier.None;
        }
    }
}
