using CoralLedger.Blue.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Infrastructure.Data.Seeding;

/// <summary>
/// Seeds a default tenant for single-tenant installations and development
/// </summary>
public static class DefaultTenantSeeder
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static async Task<Tenant> SeedAsync(MarineDbContext context)
    {
        // Check if a default tenant exists
        var existingTenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Slug == "bahamas")
            .ConfigureAwait(false);

        if (existingTenant != null)
            return existingTenant;

        // Create default Bahamas tenant
        var bahamasEezBoundary = CreateBahamasEezBoundary();
        var tenant = Tenant.Create(
            name: "Bahamas Marine Conservation",
            slug: "bahamas",
            regionCode: "BS",
            description: "Marine Protected Areas and conservation efforts for the Commonwealth of the Bahamas",
            eezBoundary: bahamasEezBoundary
        );

        context.Tenants.Add(tenant);

        // Create default configuration
        var configuration = CoralLedger.Blue.Domain.Entities.TenantConfiguration.Create(tenant.Id);
        configuration.UpdateFeatureFlags(
            vesselTracking: true,
            bleachingAlerts: true,
            citizenScience: false
        );
        context.TenantConfigurations.Add(configuration);

        // Create default branding
        var branding = TenantBranding.Create(tenant.Id);
        branding.UpdateTextBranding(
            applicationTitle: "CoralLedger Blue - Bahamas",
            tagline: "Marine Intelligence for the Blue Economy",
            welcomeMessage: "Welcome to CoralLedger Blue. Monitor coral bleaching, track fishing vessels, and protect the Bahamas' marine protected areas."
        );
        context.TenantBrandings.Add(branding);

        await context.SaveChangesAsync().ConfigureAwait(false);
        
        return tenant;
    }

    private static Geometry CreateBahamasEezBoundary()
    {
        // Simplified Bahamas EEZ boundary
        // Covers the major island chains from Grand Bahama in the north to Inagua in the south
        var coordinates = new[]
        {
            new Coordinate(-79.5, 27.5),  // Northwest
            new Coordinate(-72.5, 27.5),  // Northeast
            new Coordinate(-72.5, 20.5),  // Southeast
            new Coordinate(-79.5, 20.5),  // Southwest
            new Coordinate(-79.5, 27.5)   // Close polygon
        };

        return GeometryFactory.CreatePolygon(coordinates);
    }
}
