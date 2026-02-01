using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class PatrolRouteConfiguration : IEntityTypeConfiguration<PatrolRoute>
{
    public void Configure(EntityTypeBuilder<PatrolRoute> builder)
    {
        builder.ToTable("patrol_routes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OfficerName)
            .HasMaxLength(100);

        builder.Property(x => x.OfficerId)
            .HasMaxLength(50);

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.EndTime);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.RecordingIntervalSeconds)
            .IsRequired();

        builder.Property(x => x.TotalDistanceMeters);

        builder.Property(x => x.DurationSeconds);

        builder.Property(x => x.RouteGeometry)
            .HasColumnType("geometry(LineString, 4326)");

        // Relationships
        builder.HasOne(x => x.MarineProtectedArea)
            .WithMany()
            .HasForeignKey(x => x.MarineProtectedAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Points)
            .WithOne(p => p.PatrolRoute)
            .HasForeignKey(p => p.PatrolRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Waypoints)
            .WithOne(w => w.PatrolRoute)
            .HasForeignKey(w => w.PatrolRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.RouteGeometry)
            .HasMethod("GIST");

        builder.HasIndex(x => x.StartTime);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OfficerId);
        builder.HasIndex(x => x.MarineProtectedAreaId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
