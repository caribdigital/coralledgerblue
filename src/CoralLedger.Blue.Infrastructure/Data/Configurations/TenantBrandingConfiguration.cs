using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_brandings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.CustomDomain)
            .HasMaxLength(255);

        builder.Property(e => e.LogoUrl)
            .HasMaxLength(500);

        builder.Property(e => e.FaviconUrl)
            .HasMaxLength(500);

        builder.Property(e => e.PrimaryColor)
            .HasMaxLength(7); // #RRGGBB

        builder.Property(e => e.SecondaryColor)
            .HasMaxLength(7);

        builder.Property(e => e.AccentColor)
            .HasMaxLength(7);

        builder.Property(e => e.ApplicationTitle)
            .HasMaxLength(100);

        builder.Property(e => e.Tagline)
            .HasMaxLength(200);

        builder.Property(e => e.WelcomeMessage)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .IsUnique();

        builder.HasIndex(e => e.CustomDomain);
    }
}
