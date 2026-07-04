using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Infrastructure.Data;

namespace ResetYourFuture.TestSupport;

/// <summary>
/// Factories for an <see cref="ApplicationDbContext"/> backed by test providers.
/// Each call gets an isolated store so tests never share state.
/// </summary>
public static class DbContextFactory
{
    /// <summary>
    /// EF Core InMemory provider on a uniquely-named root (default test database).
    /// </summary>
    public static ApplicationDbContext CreateInMemory(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Relational SQLite (<c>:memory:</c>) for cases the InMemory provider can't run:
    /// <c>EF.Functions.Like</c>, <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, ISO-string ordering.
    /// The caller owns the returned context AND its open connection — dispose the context
    /// (its connection is closed on dispose, dropping the in-memory database).
    /// </summary>
    public static ApplicationDbContext CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
