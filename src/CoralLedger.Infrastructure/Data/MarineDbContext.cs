using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Infrastructure.Data;

public class MarineDbContext : DbContext, IMarineDbContext
{
    public MarineDbContext(DbContextOptions<MarineDbContext> options) : base(options)
    {
    }

    public DbSet<MarineProtectedArea> MarineProtectedAreas => Set<MarineProtectedArea>();
    public DbSet<Reef> Reefs => Set<Reef>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enable PostGIS extension
        modelBuilder.HasPostgresExtension("postgis");

        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarineDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
