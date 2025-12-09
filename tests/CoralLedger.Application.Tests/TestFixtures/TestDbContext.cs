using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Tests.TestFixtures;

/// <summary>
/// In-memory test database context for unit testing handlers.
/// Does not use PostgreSQL-specific features like PostGIS.
/// </summary>
public class TestDbContext : DbContext, IMarineDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
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
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<BahamianSpecies> BahamianSpecies => Set<BahamianSpecies>();
    public DbSet<SpeciesObservation> SpeciesObservations => Set<SpeciesObservation>();
    public DbSet<SpeciesMisidentificationReport> MisidentificationReports => Set<SpeciesMisidentificationReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure MPA entity - simplified for in-memory testing
        modelBuilder.Entity<MarineProtectedArea>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Ignore(e => e.Boundary);  // Ignore geometry for in-memory
            entity.Ignore(e => e.Centroid);  // Ignore geometry for in-memory
        });

        modelBuilder.Entity<Reef>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Location);  // Ignore geometry
        });

        modelBuilder.Entity<Vessel>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<VesselPosition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Location);  // Ignore geometry
        });

        modelBuilder.Entity<VesselEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Location);  // Ignore geometry
        });

        modelBuilder.Entity<BleachingAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Location);  // Ignore geometry
        });

        modelBuilder.Entity<CitizenObservation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Location);  // Ignore geometry
        });

        modelBuilder.Entity<ObservationPhoto>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Location);  // Ignore geometry
        });

        modelBuilder.Entity<BahamianSpecies>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SpeciesObservation>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SpeciesMisidentificationReport>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        base.OnModelCreating(modelBuilder);
    }
}
