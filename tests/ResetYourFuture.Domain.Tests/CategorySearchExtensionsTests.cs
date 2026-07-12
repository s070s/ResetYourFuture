using Microsoft.EntityFrameworkCore;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Domain.Tests;

/// <summary>
/// <see cref="CategorySearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability.
/// Categories get fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class CategorySearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static Category Cat(
        int id, string nameEn, string? nameEl = null, int courses = 0, int deletedCourses = 0, int assessments = 0)
    {
        var category = new Category { Id = FixedId(id), NameEn = nameEn, NameEl = nameEl };
        for (var i = 0; i < courses; i++)
            category.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = $"c{i}-{nameEn}" });
        // Soft-deleted courses must not affect the coursecount sort (matches displayed counts).
        for (var i = 0; i < deletedCourses; i++)
            category.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = $"dc{i}-{nameEn}", IsDeleted = true });
        for (var i = 0; i < assessments; i++)
            category.AssessmentDefinitions.Add(new AssessmentDefinition
            {
                Id = Guid.NewGuid(),
                TitleEn = $"a{i}-{nameEn}",
                Key = $"k{i}-{nameEn}",
                SchemaJson = "{}"
            });
        return category;
    }

    /// <summary>Seeds categories; CreatedAt is stamped on insert by the audit fields,
    /// so it is reassigned (2020, 2021, … in argument order) on a second save.</summary>
    private static async Task<ApplicationDbContext> SeedAsync(params Category[] categories)
    {
        var db = DbContextFactory.CreateInMemory();
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();
        for (var i = 0; i < categories.Length; i++)
            categories[i].CreatedAt = new DateTimeOffset(2020 + i, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();
        return db;
    }

    // Seed (Ids ascending A<B<C; CreatedAt 2020/2021/2022 in order):
    //   A "Arts"    el "Τέχνες",  2 live + 2 deleted courses, 0 assessments
    //   B "Biology" el null,      0 courses,                  2 assessments
    //   C "Careers" el "Καριέρα", 1 live course,              1 assessment
    [Theory]
    [InlineData("nameen", "asc", "A,B,C")]
    [InlineData("nameen", "desc", "C,B,A")]
    [InlineData("nameel", "asc", "B,C,A")] // null first, then Καριέρα < Τέχνες
    [InlineData("nameel", "desc", "A,C,B")]
    [InlineData("coursecount", "asc", "B,C,A")] // deleted courses excluded: A counts 2, not 4
    [InlineData("coursecount", "desc", "A,C,B")]
    [InlineData("assessmentcount", "asc", "A,C,B")]
    [InlineData("assessmentcount", "desc", "B,C,A")]
    [InlineData("createdat", "asc", "A,B,C")]
    [InlineData("createdat", "desc", "C,B,A")]
    [InlineData(null, null, "A,B,C")]      // default = nameen asc (pre-sorting behavior)
    [InlineData("unknown", "asc", "A,B,C")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Cat(1, "Arts", "Τέχνες", courses: 2, deletedCourses: 2),
            Cat(2, "Biology", null, assessments: 2),
            Cat(3, "Careers", "Καριέρα", courses: 1, assessments: 1));

        var letters = await db.Categories
            .ApplySort(sortBy, sortDir)
            .Select(x => x.NameEn.Substring(0, 1))
            .ToListAsync();

        string.Join(",", letters).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Cat(2, "Same"),
            Cat(1, "Same"));

        var ids = await db.Categories.ApplySort("nameen", "asc").Select(c => c.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("nameen")]
    [InlineData("nameel")]
    [InlineData("coursecount")]
    [InlineData("assessmentcount")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.Categories.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
