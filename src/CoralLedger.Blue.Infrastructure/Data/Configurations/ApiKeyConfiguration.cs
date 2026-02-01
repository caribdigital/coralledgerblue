using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.KeyHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.KeyPrefix)
            .IsRequired()
            .HasMaxLength(12);

        builder.Property(e => e.Scopes)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.RevocationReason)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.ApiClientId);

        builder.HasIndex(e => e.KeyHash);

        builder.HasIndex(e => e.IsActive);

        builder.HasIndex(e => e.ExpiresAt);

        builder.HasIndex(e => e.LastUsedAt);

        builder.HasIndex(e => e.CreatedAt);

        // Relationships
        builder.HasOne(e => e.ApiClient)
            .WithMany(c => c.ApiKeys)
            .HasForeignKey(e => e.ApiClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
