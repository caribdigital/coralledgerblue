using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CoralLedger.Blue.Application.Common.Interfaces;

public interface IMarineDbContext
{
    DbSet<MarineProtectedArea> MarineProtectedAreas { get; }
    DbSet<Reef> Reefs { get; }
    DbSet<Vessel> Vessels { get; }
    DbSet<VesselPosition> VesselPositions { get; }
    DbSet<VesselEvent> VesselEvents { get; }
    DbSet<BleachingAlert> BleachingAlerts { get; }
    DbSet<CitizenObservation> CitizenObservations { get; }
    DbSet<ObservationPhoto> ObservationPhotos { get; }
    DbSet<AlertRule> AlertRules { get; }
    DbSet<Alert> Alerts { get; }
    DbSet<BahamianSpecies> BahamianSpecies { get; }
    DbSet<SpeciesObservation> SpeciesObservations { get; }
    DbSet<SpeciesMisidentificationReport> MisidentificationReports { get; }
    DbSet<NLQAuditLog> NLQAuditLogs { get; }
    DbSet<PatrolRoute> PatrolRoutes { get; }
    DbSet<PatrolRoutePoint> PatrolRoutePoints { get; }
    DbSet<PatrolWaypoint> PatrolWaypoints { get; }

    /// <summary>
    /// Provides access to database related operations like raw SQL execution
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
