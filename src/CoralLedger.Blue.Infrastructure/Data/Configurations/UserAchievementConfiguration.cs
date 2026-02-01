using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("user_achievements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CitizenEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.AchievementKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.CurrentProgress)
            .IsRequired();

        builder.Property(x => x.TargetProgress)
            .IsRequired();

        builder.Property(x => x.IsCompleted)
            .IsRequired();

        builder.Property(x => x.PointsAwarded)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.CitizenEmail);
        builder.HasIndex(x => x.AchievementKey);
        builder.HasIndex(x => x.IsCompleted);
        
        // Unique constraint - one achievement per user
        builder.HasIndex(x => new { x.CitizenEmail, x.AchievementKey })
            .IsUnique();
    }
}
