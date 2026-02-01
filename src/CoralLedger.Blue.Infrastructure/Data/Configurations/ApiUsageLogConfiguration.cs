using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoralLedger.Blue.Infrastructure.Data.Configurations;

public class ApiUsageLogConfiguration : IEntityTypeConfiguration<ApiUsageLog>
{
    public void Configure(EntityTypeBuilder<ApiUsageLog> builder)
    {
        builder.ToTable("api_usage_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Endpoint)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.HttpMethod)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        // Indexes for analytics queries
        builder.HasIndex(e => e.ApiClientId);

        builder.HasIndex(e => e.ApiKeyId);

        builder.HasIndex(e => e.Timestamp);

        builder.HasIndex(e => new { e.ApiClientId, e.Timestamp });

        builder.HasIndex(e => new { e.Endpoint, e.Timestamp });

        builder.HasIndex(e => e.StatusCode);

        // Relationships
        builder.HasOne(e => e.ApiClient)
            .WithMany(c => c.UsageLogs)
            .HasForeignKey(e => e.ApiClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ApiKey)
            .WithMany()
            .HasForeignKey(e => e.ApiKeyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
