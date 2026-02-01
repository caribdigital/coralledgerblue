using CoralLedger.Blue.Domain.Common;

namespace CoralLedger.Blue.Domain.Entities;

public class UserPoints : BaseEntity, IAuditableEntity
{
    public string CitizenEmail { get; private set; } = string.Empty;
    public int TotalPoints { get; private set; }
    public int WeeklyPoints { get; private set; }
    public int MonthlyPoints { get; private set; }
    public DateTime LastPointsEarned { get; private set; }
    public DateTime? WeeklyResetAt { get; private set; }
    public DateTime? MonthlyResetAt { get; private set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private UserPoints() { }

    public static UserPoints Create(string citizenEmail)
    {
        if (string.IsNullOrWhiteSpace(citizenEmail))
            throw new ArgumentException("Citizen email is required", nameof(citizenEmail));

        return new UserPoints
        {
            Id = Guid.NewGuid(),
            CitizenEmail = citizenEmail,
            TotalPoints = 0,
            WeeklyPoints = 0,
            MonthlyPoints = 0,
            LastPointsEarned = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddPoints(int points, DateTime? earnedAt = null)
    {
        if (points < 0)
            throw new ArgumentException("Points must be positive", nameof(points));

        var now = earnedAt ?? DateTime.UtcNow;
        
        // Reset weekly points if needed (every Monday)
        if (WeeklyResetAt.HasValue && now > WeeklyResetAt.Value)
        {
            WeeklyPoints = 0;
        }
        
        // Reset monthly points if needed
        if (MonthlyResetAt.HasValue && now > MonthlyResetAt.Value)
        {
            MonthlyPoints = 0;
        }

        TotalPoints += points;
        WeeklyPoints += points;
        MonthlyPoints += points;
        LastPointsEarned = now;
        
        // Set next reset dates
        WeeklyResetAt = GetNextMonday(now);
        MonthlyResetAt = GetNextMonth(now);
        
        ModifiedAt = DateTime.UtcNow;
    }

    public void DeductPoints(int points, string reason)
    {
        if (points < 0)
            throw new ArgumentException("Points must be positive", nameof(points));

        // Don't allow negative totals
        TotalPoints = Math.Max(0, TotalPoints - points);
        WeeklyPoints = Math.Max(0, WeeklyPoints - points);
        MonthlyPoints = Math.Max(0, MonthlyPoints - points);
        
        ModifiedAt = DateTime.UtcNow;
    }

    private static DateTime GetNextMonday(DateTime from)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7; // Next week if today is Monday
        return from.Date.AddDays(daysUntilMonday);
    }

    private static DateTime GetNextMonth(DateTime from)
    {
        return new DateTime(from.Year, from.Month, 1).AddMonths(1);
    }
}
