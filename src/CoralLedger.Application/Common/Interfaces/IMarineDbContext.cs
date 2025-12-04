using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CoralLedger.Application.Common.Interfaces;

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

    /// <summary>
    /// Provides access to database related operations like raw SQL execution
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
