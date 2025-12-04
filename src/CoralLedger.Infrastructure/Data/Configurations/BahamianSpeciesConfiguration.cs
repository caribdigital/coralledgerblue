using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Infrastructure.Data.Configurations;

public class BahamianSpeciesConfiguration : IEntityTypeConfiguration<BahamianSpecies>
{
    public void Configure(EntityTypeBuilder<BahamianSpecies> builder)
    {
        builder.ToTable("bahamian_species");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScientificName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.CommonName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.LocalName)
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ConservationStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IsInvasive)
            .HasDefaultValue(false);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.IdentificationTips)
            .HasMaxLength(1000);

        builder.Property(x => x.Habitat)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(x => x.ScientificName).IsUnique();
        builder.HasIndex(x => x.CommonName);
        builder.HasIndex(x => x.LocalName);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.ConservationStatus);
        builder.HasIndex(x => x.IsInvasive);
    }
}
