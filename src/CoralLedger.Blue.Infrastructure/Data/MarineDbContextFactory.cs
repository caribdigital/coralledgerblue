using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoralLedger.Blue.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating MarineDbContext instances for EF Core migrations
/// </summary>
public class MarineDbContextFactory : IDesignTimeDbContextFactory<MarineDbContext>
{
    public MarineDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MarineDbContext>();
        
        // Use a dummy connection string for migrations - actual connection is from configuration
        optionsBuilder.UseNpgsql("Host=localhost;Database=coralledger;Username=postgres;Password=postgres",
            npgsqlOptions => npgsqlOptions.UseNetTopologySuite());

        return new MarineDbContext(optionsBuilder.Options);
    }
}
