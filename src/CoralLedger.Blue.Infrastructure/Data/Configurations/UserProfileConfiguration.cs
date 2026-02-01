using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CitizenEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CitizenName)
            .HasMaxLength(100);

        builder.Property(x => x.Tier)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TotalObservations)
            .IsRequired();

        builder.Property(x => x.VerifiedObservations)
            .IsRequired();

        builder.Property(x => x.RejectedObservations)
            .IsRequired();

        builder.Property(x => x.AccuracyRate)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.CitizenEmail)
            .IsUnique();

        builder.HasIndex(x => x.Tier);
        builder.HasIndex(x => x.TotalObservations);
        builder.HasIndex(x => x.VerifiedObservations);
    }
}
