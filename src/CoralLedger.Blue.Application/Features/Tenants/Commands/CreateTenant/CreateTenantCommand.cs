using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Tenants.DTOs;
using CoralLedger.Blue.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Blue.Application.Features.Tenants.Commands.CreateTenant;

public record CreateTenantCommand(
    string Name,
    string Slug,
    string? Description = null,
    string? RegionCode = null
) : IRequest<CreateTenantResult>;

public record CreateTenantResult(
    bool Success,
    TenantDto? Tenant = null,
    string? Error = null);

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, CreateTenantResult>
{
    private readonly IMarineDbContext _context;
    private readonly ILogger<CreateTenantCommandHandler> _logger;

    public CreateTenantCommandHandler(
        IMarineDbContext context,
        ILogger<CreateTenantCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CreateTenantResult> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if slug already exists
            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Slug == request.Slug.ToLowerInvariant(), cancellationToken)
                .ConfigureAwait(false);

            if (existingTenant != null)
            {
                return new CreateTenantResult(false, Error: $"A tenant with slug '{request.Slug}' already exists.");
            }

            // Create tenant
            var tenant = Tenant.Create(
                request.Name,
                request.Slug,
                request.RegionCode,
                request.Description);

            _context.Tenants.Add(tenant);

            // Create default configuration
            var configuration = CoralLedger.Blue.Domain.Entities.TenantConfiguration.Create(tenant.Id);
            _context.TenantConfigurations.Add(configuration);

            // Create default branding
            var branding = TenantBranding.Create(tenant.Id);
            _context.TenantBrandings.Add(branding);

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created tenant {TenantName} with slug {Slug}", tenant.Name, tenant.Slug);

            // Reload with relationships
            var tenantDto = await _context.Tenants
                .Where(t => t.Id == tenant.Id)
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

            return new CreateTenantResult(Success: true, Tenant: tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tenant");
            return new CreateTenantResult(false, Error: ex.Message);
        }
    }
}
