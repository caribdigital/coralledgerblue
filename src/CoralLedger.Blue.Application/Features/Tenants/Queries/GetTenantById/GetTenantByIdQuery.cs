using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Tenants.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Tenants.Queries.GetTenantById;

public record GetTenantByIdQuery(Guid TenantId) : IRequest<GetTenantByIdResult>;

public record GetTenantByIdResult(
    bool Success,
    TenantDto? Tenant = null,
    string? Error = null);

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, GetTenantByIdResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<GetTenantByIdQueryHandler> _logger;

    public GetTenantByIdQueryHandler(
        IMarineDbContext context,
        ILogger<GetTenantByIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GetTenantByIdResult> Handle(
        GetTenantByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _context.Tenants
                .Where(t => t.Id == request.TenantId)
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
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (tenant == null)
            {
                return new GetTenantByIdResult(false, Error: "Tenant not found");
            }

            return new GetTenantByIdResult(Success: true, Tenant: tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenant {TenantId}", request.TenantId);
            return new GetTenantByIdResult(false, Error: ex.Message);
        }
    }
}
