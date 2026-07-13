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
/// <see cref="CourseReviewSearchExtensions.ApplySort"/> run as EF queries against the InMemory
/// provider, plus a ToQueryString guard for SQL Server translatability. Reviews get fixed
/// ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class CourseReviewSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static CourseReview Review(int id, int rating, ReviewStatus status, int createdYear) => new()
    {
        Id = FixedId(id),
        CourseId = Guid.NewGuid(),
        UserId = "u1",
        Rating = rating,
        Body = "b",
        Status = status,
        CreatedAt = new DateTimeOffset(createdYear, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };

    private static async Task<ApplicationDbContext> SeedAsync(params CourseReview[] items)
    {
        var db = DbContextFactory.CreateInMemory();
        db.CourseReviews.AddRange(items);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<string> OrderedIdsCsv(ApplicationDbContext db, string? sortBy, string? sortDir)
    {
        var ids = await db.CourseReviews.ApplySort(sortBy, sortDir).Select(r => r.Id).ToListAsync();
        return string.Join(",", ids.Select(id => id.ToString("N").TrimStart('0')));
    }

    // Seed (Ids ascending 1<2<3):
    //   1: rating 3, Pending,  2020
    //   2: rating 5, Approved, 2022
    //   3: rating 1, Rejected, 2021
    [Theory]
    [InlineData("rating", "asc", "3,1,2")]
    [InlineData("rating", "desc", "2,1,3")]
    [InlineData("status", "asc", "1,2,3")]   // Pending(1) < Approved(2) < Rejected(3)
    [InlineData("status", "desc", "3,2,1")]
    [InlineData("createdat", "asc", "1,3,2")]
    [InlineData("createdat", "desc", "2,3,1")]
    [InlineData(null, null, "2,3,1")]        // default = createdat desc
    [InlineData("unknown", "asc", "2,3,1")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Review(1, 3, ReviewStatus.Pending, 2020),
            Review(2, 5, ReviewStatus.Approved, 2022),
            Review(3, 1, ReviewStatus.Rejected, 2021));

        (await OrderedIdsCsv(db, sortBy, sortDir)).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Review(2, 4, ReviewStatus.Pending, 2020),
            Review(1, 4, ReviewStatus.Pending, 2020));

        var ids = await db.CourseReviews.ApplySort("rating", "asc").Select(r => r.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("status")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.CourseReviews.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
