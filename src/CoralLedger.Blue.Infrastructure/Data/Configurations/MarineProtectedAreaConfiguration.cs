using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class MarineProtectedAreaConfiguration : IEntityTypeConfiguration<MarineProtectedArea>
{
    public void Configure(EntityTypeBuilder<MarineProtectedArea> builder)
    {
        builder.ToTable("marine_protected_areas");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.LocalName)
            .HasMaxLength(200);

        builder.Property(e => e.WdpaId)
            .HasMaxLength(50);

        // Spatial columns - using Geography for accurate distance calculations
        builder.Property(e => e.Boundary)
            .HasColumnType("geometry(Geometry, 4326)")
            .IsRequired();

        // 4-tier geometry simplification for map performance
        builder.Property(e => e.BoundarySimplifiedDetail)
            .HasColumnType("geometry(Geometry, 4326)");

        builder.Property(e => e.BoundarySimplifiedMedium)
            .HasColumnType("geometry(Geometry, 4326)");

        builder.Property(e => e.BoundarySimplifiedLow)
            .HasColumnType("geometry(Geometry, 4326)");

        builder.Property(e => e.Centroid)
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(e => e.AreaSquareKm)
            .HasPrecision(18, 6);

        builder.Property(e => e.WdpaLastSync);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ProtectionLevel)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.IslandGroup)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ManagingAuthority)
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.TenantId)
            .IsRequired();

        // Spatial index for efficient querying
        builder.HasIndex(e => e.Boundary)
            .HasMethod("GIST");

        builder.HasIndex(e => e.Centroid)
            .HasMethod("GIST");

        // Regular indexes
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.WdpaId).IsUnique();
        builder.HasIndex(e => e.IslandGroup);
        builder.HasIndex(e => e.ProtectionLevel);
        builder.HasIndex(e => e.TenantId);

        // Relationship
        builder.HasMany(e => e.Reefs)
            .WithOne(r => r.MarineProtectedArea)
            .HasForeignKey(r => r.MarineProtectedAreaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
