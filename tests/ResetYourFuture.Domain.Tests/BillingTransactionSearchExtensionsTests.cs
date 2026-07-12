using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="BillingTransactionSearchExtensions.ApplySort"/> run as EF queries
/// against the InMemory provider, plus a ToQueryString guard for SQL Server
/// translatability. Transactions get fixed ascending Ids so ThenBy(Id)
/// tie-breaks are deterministic.
/// </summary>
public class BillingTransactionSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static BillingTransaction Tx(int id, string planName, decimal amount, int createdYear)
    {
        var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = planName };
        return new BillingTransaction
        {
            Id = FixedId(id),
            UserId = "u1",
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan,
            Amount = amount,
            Description = "d",
            CreatedAt = new DateTime(createdYear, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static async Task<ApplicationDbContext> SeedAsync(params BillingTransaction[] transactions)
    {
        var db = DbContextFactory.CreateInMemory();
        db.BillingTransactions.AddRange(transactions);
        await db.SaveChangesAsync();
        return db;
    }

    // Seed (Ids ascending 1<2<3):
    //   1: plan "Pro",   29.99, 2020
    //   2: plan "Free",  0.00, 2022
    //   3: plan "Plus",  9.99, 2021
    [Theory]
    [InlineData("plan", "asc", "2,3,1")]
    [InlineData("plan", "desc", "1,3,2")]
    [InlineData("amount", "asc", "2,3,1")]
    [InlineData("amount", "desc", "1,3,2")]
    [InlineData("createdat", "asc", "1,3,2")]
    [InlineData("createdat", "desc", "2,3,1")]
    [InlineData(null, null, "2,3,1")]      // default = createdat desc (pre-sorting behavior)
    [InlineData("unknown", "asc", "2,3,1")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Tx(1, "Pro", 29.99m, 2020),
            Tx(2, "Free", 0m, 2022),
            Tx(3, "Plus", 9.99m, 2021));

        var ids = await db.BillingTransactions
            .ApplySort(sortBy, sortDir)
            .Select(t => t.Id)
            .ToListAsync();

        string.Join(",", ids.Select(id => id.ToString("N").TrimStart('0'))).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Tx(2, "Same", 5m, 2020),
            Tx(1, "Same", 5m, 2020));

        var ids = await db.BillingTransactions.ApplySort("amount", "asc").Select(t => t.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("amount")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.BillingTransactions.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
