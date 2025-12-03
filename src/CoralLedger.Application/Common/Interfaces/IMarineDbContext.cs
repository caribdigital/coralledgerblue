using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Common.Interfaces;

public interface IMarineDbContext
{
    DbSet<MarineProtectedArea> MarineProtectedAreas { get; }
    DbSet<Reef> Reefs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
