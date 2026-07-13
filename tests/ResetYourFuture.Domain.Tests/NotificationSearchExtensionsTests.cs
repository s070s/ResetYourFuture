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
/// <see cref="NotificationSearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability.
/// Notifications get fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class NotificationSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static Notification Item(int id, bool isRead, int createdYear) => new()
    {
        Id = FixedId(id),
        UserId = "u1",
        Type = NotificationType.ChatMessage,
        TitleKey = "ChatMessageReceived",
        IsRead = isRead,
        CreatedAt = new DateTimeOffset(createdYear, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };

    private static async Task<ApplicationDbContext> SeedAsync(params Notification[] items)
    {
        var db = DbContextFactory.CreateInMemory();
        db.Notifications.AddRange(items);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<string> OrderedIdsCsv(ApplicationDbContext db, string? sortBy, string? sortDir)
    {
        var ids = await db.Notifications.ApplySort(sortBy, sortDir).Select(n => n.Id).ToListAsync();
        return string.Join(",", ids.Select(id => id.ToString("N").TrimStart('0')));
    }

    // Seed (Ids ascending 1<2<3):
    //   1: unread, 2020
    //   2: read,   2022
    //   3: unread, 2021
    [Theory]
    [InlineData("isread", "asc", "1,3,2")]  // unread(false) first
    [InlineData("isread", "desc", "2,1,3")]
    [InlineData("createdat", "asc", "1,3,2")]
    [InlineData("createdat", "desc", "2,3,1")]
    [InlineData(null, null, "2,3,1")]       // default = createdat desc (newest first)
    [InlineData("unknown", "asc", "2,3,1")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Item(1, isRead: false, createdYear: 2020),
            Item(2, isRead: true, createdYear: 2022),
            Item(3, isRead: false, createdYear: 2021));

        (await OrderedIdsCsv(db, sortBy, sortDir)).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Item(2, isRead: false, createdYear: 2020),
            Item(1, isRead: false, createdYear: 2020));

        var ids = await db.Notifications.ApplySort("createdat", "asc").Select(n => n.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("isread")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.Notifications.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
