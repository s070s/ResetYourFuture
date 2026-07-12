using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="BlogArticleSearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability.
/// Articles get fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class BlogArticleSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static BlogArticle Article(
        int id, string title, string slug, string author,
        bool published = false, DateTimeOffset? publishedAt = null) =>
        new()
        {
            Id = FixedId(id),
            TitleEn = title,
            Slug = slug,
            SummaryEn = "s",
            ContentEn = "c",
            AuthorName = author,
            IsPublished = published,
            PublishedAt = publishedAt
        };

    /// <summary>Seeds articles; CreatedAt is stamped on insert by the audit fields,
    /// so it is reassigned (2020, 2021, … in argument order) on a second save.</summary>
    private static async Task<ApplicationDbContext> SeedAsync(params BlogArticle[] articles)
    {
        var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.AddRange(articles);
        await db.SaveChangesAsync();
        for (var i = 0; i < articles.Length; i++)
            articles[i].CreatedAt = new DateTimeOffset(2020 + i, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();
        return db;
    }

    // Seed (Ids ascending A<B<C; CreatedAt 2020/2021/2022 in order):
    //   A "Alpha"   slug "zz-alpha", author "Mira", draft,     no publish date
    //   B "Bravo"   slug "mm-bravo", author "Alex", published, 2023
    //   C "Charlie" slug "aa-charl", author "Zoe",  published, 2021
    [Theory]
    [InlineData("title", "asc", "A,B,C")]
    [InlineData("title", "desc", "C,B,A")]
    [InlineData("slug", "asc", "C,B,A")]
    [InlineData("slug", "desc", "A,B,C")]
    [InlineData("author", "asc", "B,A,C")]
    [InlineData("author", "desc", "C,A,B")]
    [InlineData("status", "asc", "A,B,C")]      // draft first; published tie breaks by Id
    [InlineData("status", "desc", "B,C,A")]
    [InlineData("publishedat", "asc", "A,C,B")] // null (draft) sorts first ascending
    [InlineData("publishedat", "desc", "B,C,A")]
    [InlineData("createdat", "asc", "A,B,C")]
    [InlineData("createdat", "desc", "C,B,A")]
    [InlineData(null, null, "C,B,A")]           // default = createdat desc (pre-sorting behavior)
    [InlineData("unknown", "asc", "C,B,A")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Article(1, "Alpha", "zz-alpha", "Mira"),
            Article(2, "Bravo", "mm-bravo", "Alex", published: true, publishedAt: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Article(3, "Charlie", "aa-charl", "Zoe", published: true, publishedAt: new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        var letters = await db.BlogArticles
            .ApplySort(sortBy, sortDir)
            .Select(x => x.TitleEn.Substring(0, 1))
            .ToListAsync();

        string.Join(",", letters).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Article(2, "Same", "s2", "Ann"),
            Article(1, "Same", "s1", "Ann"));

        var ids = await db.BlogArticles.ApplySort("title", "asc").Select(a => a.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("title")]
    [InlineData("slug")]
    [InlineData("author")]
    [InlineData("status")]
    [InlineData("publishedat")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.BlogArticles.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
