using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("tenant_users");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(254); // RFC 5321

        builder.Property(e => e.FullName)
            .HasMaxLength(200);

        builder.Property(e => e.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.TenantId);

        builder.HasIndex(e => e.Email);

        builder.HasIndex(e => new { e.TenantId, e.Email })
            .IsUnique();

        builder.HasIndex(e => e.IsActive);

        // Relationships
        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
