using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="ScheduledSessionSearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability. Sessions get
/// fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class ScheduledSessionSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static ScheduledSession Session(int id, string title, ScheduledSessionStatus status, int startOffsetHours) => new()
    {
        Id = FixedId(id),
        HostUserId = "host-1",
        TitleEn = title,
        Status = status,
        StartsAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(startOffsetHours)
    };

    private static async Task<ApplicationDbContext> SeedAsync(params ScheduledSession[] sessions)
    {
        var db = DbContextFactory.CreateInMemory();
        db.ScheduledSessions.AddRange(sessions);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<string> OrderedIdsCsv(ApplicationDbContext db, string? sortBy, string? sortDir)
    {
        var ids = await db.ScheduledSessions.ApplySort(sortBy, sortDir).Select(s => s.Id).ToListAsync();
        return string.Join(",", ids.Select(id => id.ToString("N").TrimStart('0')));
    }

    // Seed (Ids ascending 1<2<3):
    //   1: "Beta",  Live,      starts +3h
    //   2: "Alpha", Scheduled, starts +1h
    //   3: "Gamma", Cancelled, starts +2h
    [Theory]
    [InlineData("titleen", "asc", "2,1,3")]
    [InlineData("titleen", "desc", "3,1,2")]
    [InlineData("status", "asc", "2,1,3")]   // Scheduled(1) < Live(2) < Cancelled(4)
    [InlineData("status", "desc", "3,1,2")]
    [InlineData("startsatutc", "asc", "2,3,1")]
    [InlineData("startsatutc", "desc", "1,3,2")]
    [InlineData(null, null, "2,3,1")]        // default = startsatutc asc
    [InlineData("unknown", "asc", "2,3,1")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Session(1, "Beta", ScheduledSessionStatus.Live, 3),
            Session(2, "Alpha", ScheduledSessionStatus.Scheduled, 1),
            Session(3, "Gamma", ScheduledSessionStatus.Cancelled, 2));

        (await OrderedIdsCsv(db, sortBy, sortDir)).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Session(2, "Same", ScheduledSessionStatus.Scheduled, 1),
            Session(1, "Same", ScheduledSessionStatus.Scheduled, 1));

        var ids = await db.ScheduledSessions.ApplySort("titleen", "asc").Select(s => s.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("titleen")]
    [InlineData("status")]
    [InlineData("startsatutc")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.ScheduledSessions.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
