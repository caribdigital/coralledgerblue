using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

public class UserAchievement : BaseEntity, IAuditableEntity
{
    public string CitizenEmail { get; private set; } = string.Empty;
    public string AchievementKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int CurrentProgress { get; private set; }
    public int TargetProgress { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int PointsAwarded { get; private set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private UserAchievement() { }

    public static UserAchievement Create(
        string citizenEmail,
        string achievementKey,
        string title,
        int targetProgress,
        string? description = null,
        int pointsAwarded = 0)
    {
        if (string.IsNullOrWhiteSpace(citizenEmail))
            throw new ArgumentException("Citizen email is required", nameof(citizenEmail));
        if (string.IsNullOrWhiteSpace(achievementKey))
            throw new ArgumentException("Achievement key is required", nameof(achievementKey));
        if (targetProgress <= 0)
            throw new ArgumentException("Target progress must be positive", nameof(targetProgress));

        return new UserAchievement
        {
            Id = Guid.NewGuid(),
            CitizenEmail = citizenEmail,
            AchievementKey = achievementKey,
            Title = title,
            Description = description,
            CurrentProgress = 0,
            TargetProgress = targetProgress,
            IsCompleted = false,
            PointsAwarded = pointsAwarded,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool UpdateProgress(int progress)
    {
        if (IsCompleted)
            return false;

        CurrentProgress = Math.Min(progress, TargetProgress);
        
        if (CurrentProgress >= TargetProgress && !IsCompleted)
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
            ModifiedAt = DateTime.UtcNow;
            return true; // Newly completed
        }

        ModifiedAt = DateTime.UtcNow;
        return false;
    }

    public int GetProgressPercentage()
    {
        if (TargetProgress == 0) return 0;
        return (int)((double)CurrentProgress / TargetProgress * 100);
    }
}
