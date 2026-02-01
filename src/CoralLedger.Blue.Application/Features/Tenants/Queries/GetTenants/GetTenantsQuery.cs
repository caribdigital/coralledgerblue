using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Tenants.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Tenants.Queries.GetTenants;

public record GetTenantsQuery : IRequest<GetTenantsResult>;

public record GetTenantsResult(
    bool Success,
    List<TenantDto>? Tenants = null,
    string? Error = null);

public class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, GetTenantsResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<GetTenantsQueryHandler> _logger;

    public GetTenantsQueryHandler(
        IMarineDbContext context,
        ILogger<GetTenantsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GetTenantsResult> Handle(
        GetTenantsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenants = await _context.Tenants
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Slug = t.Slug,
                    Description = t.Description,
                    RegionCode = t.RegionCode,
                    IsActive = t.IsActive,
                    CreatedAt = t.CreatedAt,
                    Configuration = t.Configuration == null ? null : new TenantConfigurationDto
                    {
                        EnableAutomaticMpaSync = t.Configuration.EnableAutomaticMpaSync,
                        AllowCrossTenantDataSharing = t.Configuration.AllowCrossTenantDataSharing,
                        EnableVesselTracking = t.Configuration.EnableVesselTracking,
                        EnableBleachingAlerts = t.Configuration.EnableBleachingAlerts,
                        EnableCitizenScience = t.Configuration.EnableCitizenScience
                    },
                    Branding = t.Branding == null ? null : new TenantBrandingDto
                    {
                        CustomDomain = t.Branding.CustomDomain,
                        UseCustomDomain = t.Branding.UseCustomDomain,
                        LogoUrl = t.Branding.LogoUrl,
                        PrimaryColor = t.Branding.PrimaryColor,
                        SecondaryColor = t.Branding.SecondaryColor,
                        AccentColor = t.Branding.AccentColor,
                        ApplicationTitle = t.Branding.ApplicationTitle,
                        Tagline = t.Branding.Tagline
                    }
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new GetTenantsResult(Success: true, Tenants: tenants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenants");
            return new GetTenantsResult(false, Error: ex.Message);
        }
    }
}
