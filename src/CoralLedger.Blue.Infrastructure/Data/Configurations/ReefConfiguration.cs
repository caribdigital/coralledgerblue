using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class ReefConfiguration : IEntityTypeConfiguration<Reef>
{
    public void Configure(EntityTypeBuilder<Reef> builder)
    {
        builder.ToTable("reefs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Location)
            .HasColumnType("geometry(Geometry, 4326)")
            .IsRequired();

        builder.Property(e => e.HealthStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.DepthMeters)
            .HasPrecision(10, 2);

        builder.Property(e => e.LengthKm)
            .HasPrecision(10, 2);

        builder.Property(e => e.CoralCoverPercentage)
            .HasPrecision(5, 2);

        builder.Property(e => e.BleachingPercentage)
            .HasPrecision(5, 2);

        builder.Property(e => e.TenantId)
            .IsRequired();

        // Spatial index
        builder.HasIndex(e => e.Location)
            .HasMethod("GIST");

        // Regular indexes
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.HealthStatus);
        builder.HasIndex(e => e.TenantId);
    }
}
