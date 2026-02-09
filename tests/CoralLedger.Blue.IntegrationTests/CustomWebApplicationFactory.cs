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
    /// <summary>
    /// Default admin credentials for authenticated tests
    /// </summary>
    public const string AdminEmail = "admin@coralledger.blue";
    public const string AdminPassword = "Admin123!";

    /// <summary>
    /// Gets the default tenant ID for tests that require tenant context
    /// </summary>
    public Guid? DefaultTenantId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add configuration with dummy connection string and JWT settings for testing
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:marinedb"] = "Host=localhost;Database=test;Username=test;Password=test",
                // JWT configuration for authentication tests
                ["Jwt:Secret"] = "IntegrationTestSecretKeyThatIsAtLeast32CharactersLong!",
                ["Jwt:Issuer"] = "CoralLedger.Blue.IntegrationTests",
                ["Jwt:Audience"] = "CoralLedger.Blue.IntegrationTests",
                ["Jwt:ExpirationMinutes"] = "60"
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

            // Remove Quartz hosted service to prevent background job execution and
            // LoggerFactory disposal issues during test teardown
            var quartzHostedDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("QuartzHostedService") == true ||
                           d.ImplementationType?.FullName?.Contains("QuartzHostedService") == true)
                .ToList();

            foreach (var descriptor in quartzHostedDescriptors)
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
        DefaultTenantId = SeedTestData(db);

        return host;
    }

    private Guid SeedTestData(MarineDbContext db)
    {
        // Seed the default tenant required for multi-tenant support
        var tenant = DefaultTenantSeeder.SeedAsync(db).GetAwaiter().GetResult();

        // Seed test MPA data for integration tests
        BahamasMpaSeeder.SeedAsync(db).GetAwaiter().GetResult();

        return tenant.Id;
    }

    /// <summary>
    /// Helper method to get the password reset token for a user from the database
    /// </summary>
    public async Task<string?> GetPasswordResetTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarineDbContext>();
        
        var user = await db.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        
        if (user == null)
            return null;
        
        var token = await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
        
        return token?.Token;
    }
}
