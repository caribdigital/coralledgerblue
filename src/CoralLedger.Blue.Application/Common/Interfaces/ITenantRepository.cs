using CoralLedger.Blue.Domain.Entities;

namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Repository for tenant management operations
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
    Task<List<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
