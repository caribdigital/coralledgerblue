using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class PatrolRoutePointConfiguration : IEntityTypeConfiguration<PatrolRoutePoint>
{
    public void Configure(EntityTypeBuilder<PatrolRoutePoint> builder)
    {
        builder.ToTable("patrol_route_points");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Location)
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.Property(x => x.Accuracy);

        builder.Property(x => x.Altitude);

        builder.Property(x => x.Speed);

        builder.Property(x => x.Heading);

        // Relationship
        builder.HasOne(x => x.PatrolRoute)
            .WithMany(p => p.Points)
            .HasForeignKey(x => x.PatrolRouteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.Location)
            .HasMethod("GIST");

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.PatrolRouteId);
    }
}
