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
/// <see cref="AssessmentSearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability.
/// Definitions get fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class AssessmentSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static AssessmentDefinition Definition(
        int id, string title, string key, SubscriptionTier tier = SubscriptionTier.Free,
        bool published = false, string? categoryName = null, int submissions = 0)
    {
        var definition = new AssessmentDefinition
        {
            Id = FixedId(id),
            TitleEn = title,
            Key = key,
            SchemaJson = "{}",
            RequiredTier = tier,
            IsPublished = published,
            Category = categoryName is null ? null : new Category { Id = Guid.NewGuid(), NameEn = categoryName }
        };
        for (var i = 0; i < submissions; i++)
            definition.Submissions.Add(new AssessmentSubmission
            {
                Id = Guid.NewGuid(),
                UserId = $"u{i}-{key}",
                AssessmentDefinitionId = definition.Id,
                AnswersJson = "{}"
            });
        return definition;
    }

    /// <summary>Seeds definitions; CreatedAt is stamped on insert by the audit fields,
    /// so it is reassigned (2020, 2021, … in argument order) on a second save.</summary>
    private static async Task<ApplicationDbContext> SeedAsync(params AssessmentDefinition[] definitions)
    {
        var db = DbContextFactory.CreateInMemory();
        db.AssessmentDefinitions.AddRange(definitions);
        await db.SaveChangesAsync();
        for (var i = 0; i < definitions.Length; i++)
            definitions[i].CreatedAt = new DateTimeOffset(2020 + i, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();
        return db;
    }

    // Seed (Ids ascending A<B<C; CreatedAt 2020/2021/2022 in order):
    //   A "Alpha"   key "zulu",  Free, draft,     cat "Zeta", 2 submissions
    //   B "Bravo"   key "mike",  Pro,  published, cat "Echo", 0 submissions
    //   C "Charlie" key "alfa",  Plus, published, no cat,     1 submission
    [Theory]
    [InlineData("title", "asc", "A,B,C")]
    [InlineData("title", "desc", "C,B,A")]
    [InlineData("key", "asc", "C,B,A")]
    [InlineData("key", "desc", "A,B,C")]
    [InlineData("category", "asc", "C,B,A")] // null category sorts first
    [InlineData("category", "desc", "A,B,C")]
    [InlineData("tier", "asc", "A,C,B")]
    [InlineData("tier", "desc", "B,C,A")]
    [InlineData("status", "asc", "A,B,C")]   // draft first; published tie breaks by Id
    [InlineData("status", "desc", "B,C,A")]
    [InlineData("submissions", "asc", "B,C,A")]
    [InlineData("submissions", "desc", "A,C,B")]
    [InlineData("createdat", "asc", "A,B,C")]
    [InlineData("createdat", "desc", "C,B,A")]
    [InlineData(null, null, "C,B,A")]        // default = createdat desc (pre-sorting behavior)
    [InlineData("unknown", "asc", "C,B,A")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Definition(1, "Alpha", "zulu", SubscriptionTier.Free, published: false, categoryName: "Zeta", submissions: 2),
            Definition(2, "Bravo", "mike", SubscriptionTier.Pro, published: true, categoryName: "Echo"),
            Definition(3, "Charlie", "alfa", SubscriptionTier.Plus, published: true, submissions: 1));

        var letters = await db.AssessmentDefinitions
            .ApplySort(sortBy, sortDir)
            .Select(x => x.TitleEn.Substring(0, 1))
            .ToListAsync();

        string.Join(",", letters).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Definition(2, "Same", "k2"),
            Definition(1, "Same", "k1"));

        var ids = await db.AssessmentDefinitions.ApplySort("title", "asc").Select(a => a.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("title")]
    [InlineData("key")]
    [InlineData("category")]
    [InlineData("tier")]
    [InlineData("status")]
    [InlineData("submissions")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.AssessmentDefinitions.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
