namespace CoralLedger.Blue.Application.Features.Gamification;

/// <summary>
/// Constants for gamification point awards and penalties
/// </summary>
public static class GamificationConstants
{
    // Points awarded on observation creation
    public const int BaseObservationPoints = 5;
    public const int PhotoBonusPoints = 3;
    public const int GpsBonusPoints = 2;
    public const int MpaBonusPoints = 5;

    // Points awarded/deducted on verification
    public const int VerificationBasePoints = 10;
    public const int RejectionPenaltyPoints = 5;

    // Verification type bonuses
    public const int CoralBleachingBonus = 20;
    public const int IllegalFishingBonus = 25;
    public const int WildlifeSightingBonus = 15;
    public const int ReefHealthBonus = 15;
    public const int DefaultTypeBonus = 10;

    // Photo evidence bonus on verification
    public const int PhotoEvidenceBonus = 10;

    // Badge thresholds
    public const int TenObservationsThreshold = 10;
    public const int FiftyObservationsThreshold = 50;
    public const int HundredObservationsThreshold = 100;
    public const int AccurateObserverMinVerified = 20;
    public const double AccurateObserverMinAccuracy = 90.0;
    public const int CoralExpertThreshold = 25;
    public const int MpaGuardianThreshold = 10;
    public const int WeeklyContributorThreshold = 7;
    public const int MonthlyContributorThreshold = 30;
}
