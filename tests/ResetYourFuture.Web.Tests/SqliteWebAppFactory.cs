using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Infrastructure.Data;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// A <see cref="CustomWebAppFactory"/> variant backed by a real relational SQLite database
/// instead of the EF Core InMemory provider (TEST-1). It exists so the suites whose behavior
/// depends on relational semantics — unique-index enforcement (the enrollment
/// <c>(UserId, CourseId)</c> race path), <c>EF.Functions.Like</c> translation, ISO-string date
/// ordering, and the soft-delete global query filters — run against a provider that actually
/// enforces them; InMemory silently allows all of the above, so green InMemory tests do not
/// imply the code works against SQL Server.
///
/// SQLite <c>:memory:</c> databases live only as long as a connection to them is open, and the
/// application opens/closes a fresh DbContext (hence a fresh connection) per request scope. A
/// single shared connection, opened here and kept open for the factory's lifetime, is therefore
/// held so every scope sees the same schema and data. The schema is created via
/// <c>EnsureCreated</c> from the startup path (see the SQLite branch in
/// <c>DatabaseSeedingExtensions.PrewarmAndSeedDatabaseAsync</c>) rather than migrations, because
/// the migration chain is authored for SQL Server (<c>nvarchar(max)</c> etc.) and does not
/// translate to SQLite — applying migrations is covered separately against LocalDB by
/// <c>MigrationChainTests</c>.
/// </summary>
public class SqliteWebAppFactory : CustomWebAppFactory
{
    private SqliteConnection? _connection;

    protected override void ConfigureDatabase(IServiceCollection services)
    {
        // Opened once, kept open until the factory is disposed — closing it drops the in-memory DB.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}

[CollectionDefinition("web-sqlite")]
public sealed class SqliteWebCollection : ICollectionFixture<SqliteWebAppFactory>;
