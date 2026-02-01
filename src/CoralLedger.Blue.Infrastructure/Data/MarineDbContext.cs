using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Infrastructure.Data;

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
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<BahamianSpecies> BahamianSpecies => Set<BahamianSpecies>();
    public DbSet<SpeciesObservation> SpeciesObservations => Set<SpeciesObservation>();
    public DbSet<SpeciesMisidentificationReport> MisidentificationReports => Set<SpeciesMisidentificationReport>();
    public DbSet<NLQAuditLog> NLQAuditLogs => Set<NLQAuditLog>();
    public DbSet<PatrolRoute> PatrolRoutes => Set<PatrolRoute>();
    public DbSet<PatrolRoutePoint> PatrolRoutePoints => Set<PatrolRoutePoint>();
    public DbSet<PatrolWaypoint> PatrolWaypoints => Set<PatrolWaypoint>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiUsageLog> ApiUsageLogs => Set<ApiUsageLog>();
    
    // Gamification
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<UserPoints> UserPoints => Set<UserPoints>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enable PostGIS extension
        modelBuilder.HasPostgresExtension("postgis");

        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarineDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
