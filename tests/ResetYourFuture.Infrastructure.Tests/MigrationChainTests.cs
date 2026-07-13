using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ResetYourFuture.Infrastructure.Tests;

/// <summary>
/// Guards the migration chain the integration suites never exercise (TEST-1): the SQLite-backed
/// integration host builds its schema with <c>EnsureCreated</c>, and the InMemory host runs no
/// migrations at all, so nothing else verifies that the SQL Server migrations are complete and
/// still apply.
///
/// <para><see cref="Model_HasNoPendingChanges_AgainstSqlServer"/> is provider-correct and needs no
/// database — it catches the most common migration bug (a model change with no matching migration).
/// <see cref="FullMigrationChain_AppliesCleanly_AgainstLocalDb"/> actually applies every migration
/// to a throwaway LocalDB database; it self-skips when LocalDB is unreachable (CI, non-Windows), the
/// same opt-out shape the Ollama live smoke test uses, so it runs automatically on developer machines
/// without breaking hosts that lack SQL Server.</para>
/// </summary>
public class MigrationChainTests(ITestOutputHelper output)
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string LocalDbServer = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;";

    [Fact]
    public void Model_HasNoPendingChanges_AgainstSqlServer()
    {
        // No connection is opened — HasPendingModelChanges only compares the runtime model to the
        // snapshot, but it must be built under the SQL Server provider so the comparison uses the
        // same type mappings the snapshot was generated with.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(LocalDbServer)
            .Options;
        using var db = new ApplicationDbContext(options);

        db.Database.HasPendingModelChanges().ShouldBeFalse(
            "The EF model has changes with no matching migration — run 'dotnet ef migrations add'.");
    }

    [Fact]
    public async Task FullMigrationChain_AppliesCleanly_AgainstLocalDb()
    {
        if (!await LocalDbIsReachableAsync())
        {
            output.WriteLine("LocalDB unreachable — full migration-chain test skipped.");
            return;
        }

        var dbName = "RyfMigrationChainTest_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"{LocalDbServer}Database={dbName};")
            .Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            // Applies every migration in order, creating the database — throws on any malformed
            // migration SQL or ordering problem.
            await db.Database.MigrateAsync();

            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            applied.ShouldNotBeEmpty();
            (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
            output.WriteLine($"Applied {applied.Count} migrations to {dbName}.");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<bool> LocalDbIsReachableAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer($"{LocalDbServer}Database=master;Connect Timeout=5;")
                .Options;
            await using var probe = new ApplicationDbContext(options);
            return probe.Database.ProviderName == SqlServerProvider
                && await probe.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }
}
