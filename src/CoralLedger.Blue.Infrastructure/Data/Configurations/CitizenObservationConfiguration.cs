using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class CitizenObservationConfiguration : IEntityTypeConfiguration<CitizenObservation>
{
    public void Configure(EntityTypeBuilder<CitizenObservation> builder)
    {
        builder.ToTable("citizen_observations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Location)
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(x => x.ObservationTime)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Severity)
            .IsRequired();

        builder.Property(x => x.CitizenEmail)
            .HasMaxLength(255);

        builder.Property(x => x.CitizenName)
            .HasMaxLength(100);

        builder.Property(x => x.ApiClientId)
            .HasMaxLength(100);

        builder.Property(x => x.IsEmailVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ModerationNotes)
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(x => x.MarineProtectedArea)
            .WithMany()
            .HasForeignKey(x => x.MarineProtectedAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Reef)
            .WithMany()
            .HasForeignKey(x => x.ReefId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Photos)
            .WithOne(p => p.CitizenObservation)
            .HasForeignKey(p => p.CitizenObservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.Location)
            .HasMethod("GIST");

        builder.HasIndex(x => x.ObservationTime);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.MarineProtectedAreaId);
        builder.HasIndex(x => x.ReefId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ApiClientId);
        builder.HasIndex(x => x.IsEmailVerified);
    }
}
