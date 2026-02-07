using CoralLedger.Blue.Domain.Enums;

namespace CoralLedger.Blue.Web.Services;

/// <summary>
/// Service for displaying badge earned notifications
/// </summary>
public interface IBadgeNotificationService
{
    /// <summary>
    /// Event triggered when a badge notification should be shown
    /// </summary>
    event Action<BadgeType, string>? OnBadgeEarned;

    /// <summary>
    /// Show a badge earned notification
    /// </summary>
    void ShowBadgeEarned(BadgeType badgeType, string? description = null);
}

/// <summary>
/// Implementation of badge notification service
/// </summary>
public class BadgeNotificationService : IBadgeNotificationService
{
    public event Action<BadgeType, string>? OnBadgeEarned;

    public void ShowBadgeEarned(BadgeType badgeType, string? description = null)
    {
        OnBadgeEarned?.Invoke(badgeType, description ?? string.Empty);
    }
}
