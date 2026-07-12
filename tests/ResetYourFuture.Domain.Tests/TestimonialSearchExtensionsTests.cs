using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="TestimonialSearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability.
/// Testimonials get fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class TestimonialSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    // Testimonial is not an AuditableEntity — CreatedAt can be set directly.
    private static Testimonial Item(
        int id, string name, int order, bool active = true, int createdYear = 2020) =>
        new()
        {
            Id = FixedId(id),
            FullName = name,
            QuoteText = "q",
            DisplayOrder = order,
            IsActive = active,
            CreatedAt = new DateTimeOffset(createdYear, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

    private static async Task<ApplicationDbContext> SeedAsync(params Testimonial[] items)
    {
        var db = DbContextFactory.CreateInMemory();
        db.Testimonials.AddRange(items);
        await db.SaveChangesAsync();
        return db;
    }

    // Seed (Ids ascending A<B<C):
    //   A "Anna"  order 3, active,   2020
    //   B "Boris" order 1, inactive, 2022
    //   C "Chris" order 2, active,   2021
    [Theory]
    [InlineData("name", "asc", "A,B,C")]
    [InlineData("name", "desc", "C,B,A")]
    [InlineData("status", "asc", "B,A,C")]  // inactive first; active tie breaks by Id
    [InlineData("status", "desc", "A,C,B")]
    [InlineData("createdat", "asc", "A,C,B")]
    [InlineData("createdat", "desc", "B,C,A")]
    [InlineData("displayorder", "asc", "B,C,A")]
    [InlineData("displayorder", "desc", "A,C,B")]
    [InlineData(null, null, "B,C,A")]       // default = displayorder asc (curated order)
    [InlineData("unknown", "asc", "B,C,A")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Item(1, "Anna", order: 3, active: true, createdYear: 2020),
            Item(2, "Boris", order: 1, active: false, createdYear: 2022),
            Item(3, "Chris", order: 2, active: true, createdYear: 2021));

        var letters = await db.Testimonials
            .ApplySort(sortBy, sortDir)
            .Select(x => x.FullName.Substring(0, 1))
            .ToListAsync();

        string.Join(",", letters).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_DefaultWithEqualOrder_UsesCreatedAtThenId()
    {
        await using var db = await SeedAsync(
            Item(2, "Later", order: 1, createdYear: 2022),
            Item(1, "Early", order: 1, createdYear: 2020));

        var names = await db.Testimonials.ApplySort(null, null).Select(t => t.FullName).ToListAsync();

        names.ShouldBe(new[] { "Early", "Later" });
    }

    [Theory]
    [InlineData("name")]
    [InlineData("status")]
    [InlineData("createdat")]
    [InlineData("displayorder")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.Testimonials.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
