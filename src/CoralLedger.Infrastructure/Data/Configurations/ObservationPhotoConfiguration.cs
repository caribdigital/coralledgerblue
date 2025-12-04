using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Infrastructure.Data.Configurations;

public class ObservationPhotoConfiguration : IEntityTypeConfiguration<ObservationPhoto>
{
    public void Configure(EntityTypeBuilder<ObservationPhoto> builder)
    {
        builder.ToTable("observation_photos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BlobName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.BlobUri)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FileSizeBytes)
            .IsRequired();

        builder.Property(x => x.Caption)
            .HasMaxLength(500);

        builder.Property(x => x.DisplayOrder)
            .IsRequired();

        builder.Property(x => x.UploadedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.CitizenObservationId);
        builder.HasIndex(x => x.UploadedAt);
    }
}
