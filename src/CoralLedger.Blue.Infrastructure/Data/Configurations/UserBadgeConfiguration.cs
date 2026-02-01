using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
{
    public void Configure(EntityTypeBuilder<UserBadge> builder)
    {
        builder.ToTable("user_badges");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CitizenEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.BadgeType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EarnedAt)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(x => x.CitizenEmail);
        builder.HasIndex(x => x.BadgeType);
        builder.HasIndex(x => x.EarnedAt);
        
        // Unique constraint - can't earn the same badge twice
        builder.HasIndex(x => new { x.CitizenEmail, x.BadgeType })
            .IsUnique();
    }
}
