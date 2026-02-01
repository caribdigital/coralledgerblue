using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class UserPointsConfiguration : IEntityTypeConfiguration<UserPoints>
{
    public void Configure(EntityTypeBuilder<UserPoints> builder)
    {
        builder.ToTable("user_points");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CitizenEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.TotalPoints)
            .IsRequired();

        builder.Property(x => x.WeeklyPoints)
            .IsRequired();

        builder.Property(x => x.MonthlyPoints)
            .IsRequired();

        builder.Property(x => x.LastPointsEarned)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.CitizenEmail)
            .IsUnique();

        builder.HasIndex(x => x.TotalPoints);
        builder.HasIndex(x => x.WeeklyPoints);
        builder.HasIndex(x => x.MonthlyPoints);
    }
}
