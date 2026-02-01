using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> builder)
    {
        builder.ToTable("api_clients");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.OrganizationName)
            .HasMaxLength(200);

        builder.Property(e => e.ContactEmail)
            .HasMaxLength(254); // RFC 5321

        builder.Property(e => e.ClientId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.DeactivationReason)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.ClientId)
            .IsUnique();

        builder.HasIndex(e => e.IsActive);

        builder.HasIndex(e => e.CreatedAt);

        // Relationships
        builder.HasMany(e => e.ApiKeys)
            .WithOne(k => k.ApiClient)
            .HasForeignKey(k => k.ApiClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.UsageLogs)
            .WithOne(u => u.ApiClient)
            .HasForeignKey(u => u.ApiClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
