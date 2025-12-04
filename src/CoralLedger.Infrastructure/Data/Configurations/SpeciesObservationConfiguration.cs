using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Infrastructure.Data.Configurations;

public class SpeciesObservationConfiguration : IEntityTypeConfiguration<SpeciesObservation>
{
    public void Configure(EntityTypeBuilder<SpeciesObservation> builder)
    {
        builder.ToTable("species_observations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AiConfidenceScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.IdentifiedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(x => x.CitizenObservation)
            .WithMany()
            .HasForeignKey(x => x.CitizenObservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.BahamianSpecies)
            .WithMany(s => s.Observations)
            .HasForeignKey(x => x.BahamianSpeciesId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => new { x.CitizenObservationId, x.BahamianSpeciesId });
        builder.HasIndex(x => x.RequiresExpertVerification);
        builder.HasIndex(x => x.IsAiGenerated);
        builder.HasIndex(x => x.IdentifiedAt);
    }
}
