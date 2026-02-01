using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class TenantConfigurationConfiguration : IEntityTypeConfiguration<CoralLedger.Blue.Domain.Entities.TenantConfiguration>
{
    public void Configure(EntityTypeBuilder<CoralLedger.Blue.Domain.Entities.TenantConfiguration> builder)
    {
        builder.ToTable("tenant_configurations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.WdpaApiToken)
            .HasMaxLength(500);

        builder.Property(e => e.CustomMpaSourceUrl)
            .HasMaxLength(500);

        builder.Property(e => e.SharedDataTenantIds)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .IsUnique();
    }
}
