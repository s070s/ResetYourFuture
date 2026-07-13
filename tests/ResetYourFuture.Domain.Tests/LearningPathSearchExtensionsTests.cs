using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="LearningPathSearchExtensions.ApplySort"/> run as EF queries against the InMemory
/// provider, plus a ToQueryString guard for SQL Server translatability. Paths get fixed
/// ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class LearningPathSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static LearningPath Path(int id, string title, bool published, int displayOrder) => new()
    {
        Id = FixedId(id),
        TitleEn = title,
        IsPublished = published,
        DisplayOrder = displayOrder
    };

    /// <summary>Seeds paths; CreatedAt is stamped on insert by the audit fields, so it is
    /// reassigned (2020, 2021, … in argument order) on a second save.</summary>
    private static async Task<ApplicationDbContext> SeedAsync(params LearningPath[] paths)
    {
        var db = DbContextFactory.CreateInMemory();
        db.LearningPaths.AddRange(paths);
        await db.SaveChangesAsync();
        for (var i = 0; i < paths.Length; i++)
            paths[i].CreatedAt = new DateTimeOffset(2020 + i, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<string> OrderedIdsCsv(ApplicationDbContext db, string? sortBy, string? sortDir)
    {
        var ids = await db.LearningPaths.ApplySort(sortBy, sortDir).Select(p => p.Id).ToListAsync();
        return string.Join(",", ids.Select(id => id.ToString("N").TrimStart('0')));
    }

    // Seed (Ids ascending 1<2<3; CreatedAt 2020/2021/2022 in argument order):
    //   1: "Beta",  draft,     DisplayOrder 3
    //   2: "Alpha", published, DisplayOrder 1
    //   3: "Gamma", draft,     DisplayOrder 2
    [Theory]
    [InlineData("titleen", "asc", "2,1,3")]
    [InlineData("titleen", "desc", "3,1,2")]
    [InlineData("ispublished", "asc", "1,3,2")]  // draft first; ties break by Id
    [InlineData("ispublished", "desc", "2,1,3")]
    [InlineData("createdat", "asc", "1,2,3")]
    [InlineData("createdat", "desc", "3,2,1")]
    [InlineData("displayorder", "asc", "2,3,1")]
    [InlineData("displayorder", "desc", "1,3,2")]
    [InlineData(null, null, "2,3,1")]            // default = displayorder asc
    [InlineData("unknown", "asc", "2,3,1")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Path(1, "Beta", published: false, displayOrder: 3),
            Path(2, "Alpha", published: true, displayOrder: 1),
            Path(3, "Gamma", published: false, displayOrder: 2));

        (await OrderedIdsCsv(db, sortBy, sortDir)).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Path(2, "Same", published: false, displayOrder: 1),
            Path(1, "Same", published: false, displayOrder: 1));

        var ids = await db.LearningPaths.ApplySort("displayorder", "asc").Select(p => p.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("titleen")]
    [InlineData("ispublished")]
    [InlineData("createdat")]
    [InlineData("displayorder")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.LearningPaths.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
