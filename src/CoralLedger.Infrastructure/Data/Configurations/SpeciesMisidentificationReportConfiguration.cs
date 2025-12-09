using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Infrastructure.Data.Configurations;

public class SpeciesMisidentificationReportConfiguration : IEntityTypeConfiguration<SpeciesMisidentificationReport>
{
    public void Configure(EntityTypeBuilder<SpeciesMisidentificationReport> builder)
    {
        builder.ToTable("species_misidentification_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IncorrectScientificName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CorrectedScientificName)
            .HasMaxLength(200);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.ReporterEmail)
            .HasMaxLength(256);

        builder.Property(x => x.ReporterName)
            .HasMaxLength(100);

        builder.Property(x => x.ReviewNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.ReportedAt)
            .IsRequired();

        // Store enums as strings for readability
        builder.Property(x => x.Expertise)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Relationships
        builder.HasOne(x => x.SpeciesObservation)
            .WithMany()
            .HasForeignKey(x => x.SpeciesObservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CorrectedSpecies)
            .WithMany()
            .HasForeignKey(x => x.CorrectedSpeciesId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for querying
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReportedAt);
        builder.HasIndex(x => x.SpeciesObservationId);
        builder.HasIndex(x => x.Expertise);
    }
}
