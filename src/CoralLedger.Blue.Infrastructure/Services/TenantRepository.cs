using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Infrastructure.Services;

public class TenantRepository : ITenantRepository
{
    private readonly MarineDbContext _context;
    
    public TenantRepository(MarineDbContext context)
    {
        _context = context;
    }
    
    public async Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Include(t => t.Configuration)
            .Include(t => t.Branding)
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, cancellationToken);
    }
    
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Include(t => t.Configuration)
            .Include(t => t.Branding)
            .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant() && t.IsActive, cancellationToken);
    }
    
    public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Include(t => t.Configuration)
            .Include(t => t.Branding)
            .FirstOrDefaultAsync(t => t.Branding != null 
                && t.Branding.CustomDomain == domain.ToLowerInvariant() 
                && t.Branding.UseCustomDomain 
                && t.IsActive, 
                cancellationToken);
    }
    
    public async Task<List<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);
        return tenant;
    }
    
    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
