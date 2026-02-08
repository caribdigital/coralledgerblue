using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantUserId)
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ExpiresAt)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.RevokedAt);

        builder.Property(e => e.ReplacedByTokenId);

        // Indexes
        builder.HasIndex(e => e.TokenHash)
            .IsUnique();

        builder.HasIndex(e => e.TenantUserId);

        builder.HasIndex(e => e.ExpiresAt);

        builder.HasIndex(e => e.RevokedAt);

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
