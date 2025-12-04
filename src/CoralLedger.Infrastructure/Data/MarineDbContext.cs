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
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<VesselPosition> VesselPositions => Set<VesselPosition>();
    public DbSet<VesselEvent> VesselEvents => Set<VesselEvent>();
    public DbSet<BleachingAlert> BleachingAlerts => Set<BleachingAlert>();
    public DbSet<CitizenObservation> CitizenObservations => Set<CitizenObservation>();
    public DbSet<ObservationPhoto> ObservationPhotos => Set<ObservationPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enable PostGIS extension
        modelBuilder.HasPostgresExtension("postgis");

        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarineDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
