using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Infrastructure.Data;
using CoralLedger.Blue.Infrastructure.Data.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoralLedger.Blue.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add configuration with a dummy connection string to satisfy Aspire validation
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:marinedb"] = "Host=localhost;Database=test;Username=test;Password=test"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations
            services.RemoveAll<DbContextOptions<MarineDbContext>>();
            services.RemoveAll<MarineDbContext>();
            services.RemoveAll<IMarineDbContext>();

            // Remove any pooled DbContext factories
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.Name.Contains("MarineDbContext") ||
                           d.ServiceType.Name.Contains("DbContextPool") ||
                           d.ImplementationType?.Name.Contains("MarineDbContext") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            var dbName = "IntegrationTestDb_" + Guid.NewGuid();
            services.AddDbContext<MarineDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
                options.EnableSensitiveDataLogging();
            });

            services.AddScoped<IMarineDbContext>(sp => sp.GetRequiredService<MarineDbContext>());
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Replace the host builder configuration to avoid Aspire's database configuration
        builder.ConfigureServices(services =>
        {
            // Ensure database is seeded after host is built
        });

        var host = base.CreateHost(builder);

        // Seed the database
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        db.Database.EnsureCreated();
        SeedTestData(db);

        return host;
    }

    private static void SeedTestData(MarineDbContext db)
    {
        // Seed the default tenant required for multi-tenant support
        DefaultTenantSeeder.SeedAsync(db).GetAwaiter().GetResult();
        
        // Seed test MPA data for integration tests
        BahamasMpaSeeder.SeedAsync(db).GetAwaiter().GetResult();
    }
}
