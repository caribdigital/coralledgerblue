namespace CoralLedger.Domain.Enums;

/// <summary>
/// Trust levels for citizen scientist data submissions (Sprint 4.2 US-4.2.4).
/// Addresses Dr. Bethel Risk 3.1: GIGO - tiered trust levels for data quality.
/// </summary>
public enum TrustLevel
{
    /// <summary>
    /// New users - all submissions require moderator review
    /// </summary>
    Unverified = 0,

    /// <summary>
    /// Users with 5+ approved submissions - reduced review requirements
    /// </summary>
    Trusted = 1,

    /// <summary>
    /// Users with 25+ approved submissions and no rejections in last 10
    /// </summary>
    Expert = 2,

    /// <summary>
    /// Marine biologists, rangers, verified professionals
    /// </summary>
    Professional = 3,

    /// <summary>
    /// System administrators and moderators
    /// </summary>
    Moderator = 4
}
