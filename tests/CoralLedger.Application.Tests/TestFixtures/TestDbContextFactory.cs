using CoralLedger.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Tests.TestFixtures;

/// <summary>
/// Factory for creating in-memory test database contexts.
/// Each test gets a unique database instance to ensure isolation.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new in-memory database context for testing.
    /// </summary>
    /// <returns>A fresh IMarineDbContext with an empty database</returns>
    public static IMarineDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a new in-memory database context and disposes it when the returned scope is disposed.
    /// </summary>
    public static (IMarineDbContext Context, IDisposable Scope) CreateWithScope()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return (context, context);
    }
}
