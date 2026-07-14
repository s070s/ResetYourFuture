using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Web.Services;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class RefreshTokenPurgeServiceTests
{
    private const string UserId = "user-1";

    private static RefreshToken Token(DateTimeOffset expiresAt, DateTimeOffset? revokedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        TokenHash = Guid.NewGuid().ToString("N"),
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt
    };

    private static RefreshTokenPurgeService Build(ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        // Singleton (not Scoped): the service creates+disposes its own scope internally, and a
        // Scoped registration of an externally-owned, shared `db` would get disposed along with
        // that scope — breaking assertions the test makes against `db` afterward.
        services.AddSingleton<IApplicationDbContext>(_ => db);
        var provider = services.BuildServiceProvider();

        return new RefreshTokenPurgeService(provider, NullLogger<RefreshTokenPurgeService>.Instance);
    }

    // PurgeExpiredTokensAsync is private on the sealed BackgroundService; invoke it directly to
    // test the purge logic without waiting out the real poll interval.
    private static Task RunPurge(RefreshTokenPurgeService service, CancellationToken ct)
    {
        var method = typeof(RefreshTokenPurgeService).GetMethod(
            "PurgeExpiredTokensAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(service, [ct])!;
    }

    [Fact]
    public async Task Purge_ExpiredToken_IsDeleted()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.RefreshTokens.Add(Token(DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        await RunPurge(Build(db), CancellationToken.None);

        (await db.RefreshTokens.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Purge_ExpiredAndRevokedToken_IsDeleted()
    {
        // COMP-5: purge is safe by expiry alone — AuthApiService.RefreshAsync checks
        // ExpiresAt <= now before RevokedAt, so a dead token's revoked status is moot.
        await using var db = DbContextFactory.CreateInMemory();
        db.RefreshTokens.Add(Token(DateTimeOffset.UtcNow.AddDays(-1), revokedAt: DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        await RunPurge(Build(db), CancellationToken.None);

        (await db.RefreshTokens.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Purge_NotYetExpiredToken_IsKept()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var active = Token(DateTimeOffset.UtcNow.AddDays(1));
        db.RefreshTokens.Add(active);
        await db.SaveChangesAsync();

        await RunPurge(Build(db), CancellationToken.None);

        (await db.RefreshTokens.SingleAsync()).Id.ShouldBe(active.Id);
    }

    [Fact]
    public async Task Purge_NotYetExpiredButRevokedToken_IsKept()
    {
        // A rotated-but-not-yet-expired token must survive — it's the reuse-detection tripwire
        // (presenting it again after rotation is what SEC-1's chain-revocation catches).
        await using var db = DbContextFactory.CreateInMemory();
        var rotated = Token(DateTimeOffset.UtcNow.AddDays(3), revokedAt: DateTimeOffset.UtcNow);
        db.RefreshTokens.Add(rotated);
        await db.SaveChangesAsync();

        await RunPurge(Build(db), CancellationToken.None);

        (await db.RefreshTokens.SingleAsync()).Id.ShouldBe(rotated.Id);
    }

    [Fact]
    public async Task Purge_NoExpiredTokens_IsNoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.RefreshTokens.Add(Token(DateTimeOffset.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();

        await RunPurge(Build(db), CancellationToken.None);

        (await db.RefreshTokens.CountAsync()).ShouldBe(1);
    }
}
