namespace CoralLedger.Domain.Enums;

/// <summary>
/// User persona for AI response formatting
/// Per Dr. Bethel's accessibility requirements - different users need different response styles
/// </summary>
public enum UserPersona
{
    /// <summary>
    /// Default balanced response for general users
    /// </summary>
    General,

    /// <summary>
    /// Park ranger - Focus on enforcement, patrol routes, violations, coordinates
    /// </summary>
    Ranger,

    /// <summary>
    /// Commercial fisherman - Focus on sustainability, quotas, practical fishing info, plain language
    /// </summary>
    Fisherman,

    /// <summary>
    /// Researcher - Include data sources, methodology, statistics, confidence intervals
    /// </summary>
    Scientist,

    /// <summary>
    /// Government official - Executive summary style, policy implications, strategic recommendations
    /// </summary>
    Policymaker
}
