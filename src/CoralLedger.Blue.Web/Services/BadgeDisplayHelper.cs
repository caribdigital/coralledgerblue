using CoralLedger.Blue.Domain.Enums;

namespace CoralLedger.Blue.Web.Services;

/// <summary>
/// Provides helper methods for displaying badge information in the UI
/// </summary>
public static class BadgeDisplayHelper
{
    /// <summary>
    /// Gets the emoji icon for a badge type
    /// </summary>
    public static string GetBadgeIcon(BadgeType badge) => badge switch
    {
        BadgeType.FirstObservation => "🌊",
        BadgeType.FirstVerifiedObservation => "✅",
        BadgeType.TenObservations => "🔟",
        BadgeType.FiftyObservations => "5️⃣0️⃣",
        BadgeType.HundredObservations => "💯",
        BadgeType.SpeciesExpert => "🐠",
        BadgeType.CoralExpert => "🪸",
        BadgeType.FishExpert => "🐟",
        BadgeType.PhotoPro => "📷",
        BadgeType.AccurateObserver => "🎯",
        BadgeType.WeeklyContributor => "📅",
        BadgeType.MonthlyContributor => "🗓️",
        BadgeType.YearlyContributor => "📆",
        BadgeType.MPAGuardian => "🛡️",
        BadgeType.BleachingDetector => "🌡️",
        BadgeType.DebrisWarrior => "♻️",
        BadgeType.Helpful => "🤝",
        BadgeType.Educator => "🎓",
        _ => "⭐"
    };

    /// <summary>
    /// Gets the display name for a badge type
    /// </summary>
    public static string GetBadgeName(BadgeType badge) => badge switch
    {
        BadgeType.FirstObservation => "First Observation",
        BadgeType.FirstVerifiedObservation => "First Verified",
        BadgeType.TenObservations => "10 Observations",
        BadgeType.FiftyObservations => "50 Observations",
        BadgeType.HundredObservations => "100 Observations",
        BadgeType.SpeciesExpert => "Species Expert",
        BadgeType.CoralExpert => "Coral Expert",
        BadgeType.FishExpert => "Fish Expert",
        BadgeType.PhotoPro => "Photo Pro",
        BadgeType.AccurateObserver => "Accurate Observer",
        BadgeType.WeeklyContributor => "Weekly Contributor",
        BadgeType.MonthlyContributor => "Monthly Contributor",
        BadgeType.YearlyContributor => "Yearly Contributor",
        BadgeType.MPAGuardian => "MPA Guardian",
        BadgeType.BleachingDetector => "Bleaching Detector",
        BadgeType.DebrisWarrior => "Debris Warrior",
        BadgeType.Helpful => "Helpful",
        BadgeType.Educator => "Educator",
        _ => badge.ToString()
    };

    /// <summary>
    /// Gets the description for a badge type
    /// </summary>
    public static string GetBadgeDescription(BadgeType badge) => badge switch
    {
        BadgeType.FirstObservation => "Submitted your first marine observation",
        BadgeType.FirstVerifiedObservation => "Had your first observation verified by moderators",
        BadgeType.TenObservations => "Submitted 10 marine observations",
        BadgeType.FiftyObservations => "Submitted 50 marine observations",
        BadgeType.HundredObservations => "Submitted 100 marine observations",
        BadgeType.SpeciesExpert => "Identified multiple species with high accuracy",
        BadgeType.CoralExpert => "Specialized knowledge in coral identification",
        BadgeType.FishExpert => "Specialized knowledge in fish identification",
        BadgeType.PhotoPro => "Submitted high-quality photos consistently",
        BadgeType.AccurateObserver => "Maintained 90%+ observation accuracy",
        BadgeType.WeeklyContributor => "Contributed observations every week for a month",
        BadgeType.MonthlyContributor => "Contributed observations every month for a year",
        BadgeType.YearlyContributor => "Active contributor for over a year",
        BadgeType.MPAGuardian => "Frequent contributor to MPA monitoring",
        BadgeType.BleachingDetector => "Reported coral bleaching events",
        BadgeType.DebrisWarrior => "Reported marine debris consistently",
        BadgeType.Helpful => "Helped other users with feedback and guidance",
        BadgeType.Educator => "Contributed educational content or outreach",
        _ => "Special achievement badge"
    };

    /// <summary>
    /// Gets the requirement for earning a badge type
    /// </summary>
    public static string GetBadgeRequirement(BadgeType badge) => badge switch
    {
        BadgeType.FirstObservation => "Submit 1 observation",
        BadgeType.FirstVerifiedObservation => "Get 1 observation verified",
        BadgeType.TenObservations => "Submit 10 observations",
        BadgeType.FiftyObservations => "Submit 50 observations",
        BadgeType.HundredObservations => "Submit 100 observations",
        BadgeType.SpeciesExpert => "Identify 20+ unique species",
        BadgeType.CoralExpert => "25+ verified coral observations",
        BadgeType.FishExpert => "25+ verified fish observations",
        BadgeType.PhotoPro => "Upload 50+ quality photos",
        BadgeType.AccurateObserver => "Maintain 90%+ accuracy",
        BadgeType.WeeklyContributor => "4 consecutive weeks",
        BadgeType.MonthlyContributor => "12 consecutive months",
        BadgeType.YearlyContributor => "Active for 365+ days",
        BadgeType.MPAGuardian => "50+ MPA observations",
        BadgeType.BleachingDetector => "Report 5+ bleaching events",
        BadgeType.DebrisWarrior => "Report 10+ debris incidents",
        BadgeType.Helpful => "Awarded by moderators",
        BadgeType.Educator => "Awarded by moderators",
        _ => "Special requirement"
    };
}
