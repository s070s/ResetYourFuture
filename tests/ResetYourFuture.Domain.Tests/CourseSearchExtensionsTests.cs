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
/// <see cref="CourseSearchExtensions.ApplySort"/> run as EF queries against the
/// InMemory provider, plus a ToQueryString guard for SQL Server translatability.
/// Courses get fixed ascending Ids so ThenBy(Id) tie-breaks are deterministic.
/// </summary>
public class CourseSearchExtensionsTests
{
    private static Guid FixedId(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static Course Course(
        int id, string title, SubscriptionTier tier = SubscriptionTier.Free,
        bool published = false, string? categoryName = null, int enrollments = 0)
    {
        var course = new Course
        {
            Id = FixedId(id),
            TitleEn = title,
            RequiredTier = tier,
            IsPublished = published,
            Category = categoryName is null ? null : new Category { Id = Guid.NewGuid(), NameEn = categoryName }
        };
        for (var i = 0; i < enrollments; i++)
            course.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = $"u{i}-{title}", CourseId = course.Id });
        return course;
    }

    /// <summary>Seeds courses; CreatedAt is stamped on insert by the audit fields, so it is
    /// reassigned (2020, 2021, … in argument order) on a second save.</summary>
    private static async Task<ApplicationDbContext> SeedAsync(params Course[] courses)
    {
        var db = DbContextFactory.CreateInMemory();
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();
        for (var i = 0; i < courses.Length; i++)
            courses[i].CreatedAt = new DateTimeOffset(2020 + i, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();
        return db;
    }

    // Seed (Ids ascending A<B<C; CreatedAt 2020/2021/2022 in order):
    //   A "Alpha"   Free, draft,     cat "Zeta", 2 enrollments
    //   B "Bravo"   Pro,  published, cat "Echo", 0 enrollments
    //   C "Charlie" Plus, published, no cat,     1 enrollment
    [Theory]
    [InlineData("title", "asc", "A,B,C")]
    [InlineData("title", "desc", "C,B,A")]
    [InlineData("category", "asc", "C,B,A")] // null category sorts first
    [InlineData("category", "desc", "A,B,C")]
    [InlineData("tier", "asc", "A,C,B")]
    [InlineData("tier", "desc", "B,C,A")]
    [InlineData("status", "asc", "A,B,C")]   // draft first; published tie breaks by Id
    [InlineData("status", "desc", "B,C,A")]
    [InlineData("enrollments", "asc", "B,C,A")]
    [InlineData("enrollments", "desc", "A,C,B")]
    [InlineData("createdat", "asc", "A,B,C")]
    [InlineData("createdat", "desc", "C,B,A")]
    [InlineData(null, null, "C,B,A")]        // default = createdat desc (pre-sorting behavior)
    [InlineData("unknown", "asc", "C,B,A")]
    public async Task ApplySort_OrdersAsExpected(string? sortBy, string? sortDir, string expectedCsv)
    {
        await using var db = await SeedAsync(
            Course(1, "Alpha", SubscriptionTier.Free, published: false, categoryName: "Zeta", enrollments: 2),
            Course(2, "Bravo", SubscriptionTier.Pro, published: true, categoryName: "Echo"),
            Course(3, "Charlie", SubscriptionTier.Plus, published: true, enrollments: 1));

        var letters = await db.Courses
            .ApplySort(sortBy, sortDir)
            .Select(x => x.TitleEn.Substring(0, 1))
            .ToListAsync();

        string.Join(",", letters).ShouldBe(expectedCsv);
    }

    [Fact]
    public async Task ApplySort_EqualValues_FallBackToIdTieBreaker()
    {
        await using var db = await SeedAsync(
            Course(2, "Same"),
            Course(1, "Same"));

        var ids = await db.Courses.ApplySort("title", "asc").Select(cs => cs.Id).ToListAsync();

        ids.ShouldBe(new[] { FixedId(1), FixedId(2) });
    }

    [Theory]
    [InlineData("title")]
    [InlineData("category")]
    [InlineData("tier")]
    [InlineData("status")]
    [InlineData("enrollments")]
    [InlineData("createdat")]
    public void ApplySort_EveryKey_TranslatesToSqlServerSql(string sortBy)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        using var db = new ApplicationDbContext(options);

        db.Courses.ApplySort(sortBy, "desc").ToQueryString().ShouldContain("ORDER BY");
    }
}
