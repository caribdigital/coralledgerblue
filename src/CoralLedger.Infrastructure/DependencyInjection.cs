using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoralLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDateTimeService, DateTimeService>();

        return services;
    }

    public static void AddMarineDatabase(
        this IHostApplicationBuilder builder,
        string connectionName)
    {
        // Aspire's AddNpgsqlDbContext handles the connection, but we need to configure NTS
        // Using the configureDbContextOptions callback to add NetTopologySuite support
        builder.AddNpgsqlDbContext<MarineDbContext>(connectionName,
            configureDbContextOptions: options =>
            {
                // Configure Npgsql to use NetTopologySuite for spatial types
                // Note: This is a workaround as Aspire's API doesn't expose configureDataSourceBuilder
                options.UseNpgsql(npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                });
            });

        builder.Services.AddScoped<IMarineDbContext>(sp =>
            sp.GetRequiredService<MarineDbContext>());
    }
}
