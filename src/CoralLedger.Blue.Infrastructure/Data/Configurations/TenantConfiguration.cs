using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.RegionCode)
            .HasMaxLength(10);

        builder.Property(e => e.EezBoundary)
            .HasColumnType("geometry");

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.Slug)
            .IsUnique();

        builder.HasIndex(e => e.IsActive);

        builder.HasIndex(e => e.RegionCode);

        // Relationships
        builder.HasOne(e => e.Configuration)
            .WithOne(c => c.Tenant)
            .HasForeignKey<TenantConfiguration>(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Branding)
            .WithOne(b => b.Tenant)
            .HasForeignKey<TenantBranding>(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.MarineProtectedAreas)
            .WithOne(m => m.Tenant)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ApiClients)
            .WithOne(a => a.Tenant)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
