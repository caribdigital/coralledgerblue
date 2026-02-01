using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class BleachingAlertConfiguration : IEntityTypeConfiguration<BleachingAlert>
{
    public void Configure(EntityTypeBuilder<BleachingAlert> builder)
    {
        builder.ToTable("bleaching_alerts");

        builder.HasKey(e => e.Id);

        // Spatial column
        builder.Property(e => e.Location)
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.AlertLevel)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SeaSurfaceTemperature)
            .HasPrecision(6, 3)
            .IsRequired();

        builder.Property(e => e.SstAnomaly)
            .HasPrecision(6, 3)
            .IsRequired();

        builder.Property(e => e.HotSpot)
            .HasPrecision(6, 3);

        builder.Property(e => e.DegreeHeatingWeek)
            .HasPrecision(8, 3)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .IsRequired();

        // Spatial index
        builder.HasIndex(e => e.Location)
            .HasMethod("GIST");

        // Regular indexes
        builder.HasIndex(e => e.Date);
        builder.HasIndex(e => e.AlertLevel);
        builder.HasIndex(e => e.DegreeHeatingWeek);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.Date, e.AlertLevel });
        builder.HasIndex(e => new { e.MarineProtectedAreaId, e.Date });

        // Relationships
        builder.HasOne(e => e.MarineProtectedArea)
            .WithMany()
            .HasForeignKey(e => e.MarineProtectedAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Reef)
            .WithMany()
            .HasForeignKey(e => e.ReefId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
