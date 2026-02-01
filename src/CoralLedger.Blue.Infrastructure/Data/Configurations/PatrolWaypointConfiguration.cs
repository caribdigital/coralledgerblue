using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class PatrolWaypointConfiguration : IEntityTypeConfiguration<PatrolWaypoint>
{
    public void Configure(EntityTypeBuilder<PatrolWaypoint> builder)
    {
        builder.ToTable("patrol_waypoints");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Location)
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.WaypointType)
            .HasMaxLength(50);

        // Relationship
        builder.HasOne(x => x.PatrolRoute)
            .WithMany(p => p.Waypoints)
            .HasForeignKey(x => x.PatrolRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.Location)
            .HasMethod("GIST");

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.PatrolRouteId);
        builder.HasIndex(x => x.WaypointType);
    }
}
