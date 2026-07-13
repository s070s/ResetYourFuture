using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Infrastructure.Seeding;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public sealed class SeederTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private string TempDirWith(string fileName, string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ryf-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
        _tempDirs.Add(dir);
        return dir;
    }

    private static string NonExistentDir() => Path.Combine(Path.GetTempPath(), "ryf-missing-" + Guid.NewGuid().ToString("N"));

    // ---- SubscriptionPlanSeeder ---------------------------------------------

    [Fact]
    public async Task SubscriptionPlanSeeder_SeedsThreeTiers()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await SubscriptionPlanSeeder.SeedAsync(db, NullLogger.Instance);

        var plans = await db.SubscriptionPlans.ToListAsync();
        plans.Count.ShouldBe(3);
        plans.Select(p => p.Tier).ShouldBe(
            new[] { SubscriptionTier.Free, SubscriptionTier.Plus, SubscriptionTier.Pro }, ignoreOrder: true);
    }

    [Fact]
    public async Task SubscriptionPlanSeeder_IsIdempotent()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await SubscriptionPlanSeeder.SeedAsync(db, NullLogger.Instance);
        await SubscriptionPlanSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.SubscriptionPlans.CountAsync()).ShouldBe(3);
    }

    // ---- BlogArticleSeeder ---------------------------------------------------

    [Fact]
    public async Task BlogArticleSeeder_SeedsArticles_AndIsIdempotent()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await BlogArticleSeeder.SeedAsync(db, NullLogger.Instance);
        var firstCount = await db.BlogArticles.CountAsync();

        await BlogArticleSeeder.SeedAsync(db, NullLogger.Instance);
        var secondCount = await db.BlogArticles.CountAsync();

        firstCount.ShouldBeGreaterThan(0);
        secondCount.ShouldBe(firstCount); // articles already present → skipped, no delete/reseed
    }

    [Fact]
    public async Task BlogArticleSeeder_NeverDeletesExistingEnglishOnlyArticles()
    {
        // OPS-1 regression: the seeder used to treat "no article has a Greek title" as
        // pre-bilingual leftover data and delete + reseed it — silently destroying a real
        // operator's English-only articles. It must now leave any existing data alone.
        await using var db = DbContextFactory.CreateInMemory();
        db.BlogArticles.Add(new()
        {
            Id = Guid.NewGuid(),
            TitleEn = "A Real Operator's Article",
            TitleEl = null,
            Slug = "a-real-operators-article",
            SummaryEn = "Written by a human, not the seeder.",
            ContentEn = "Do not delete me.",
            AuthorName = "The Operator"
        });
        await db.SaveChangesAsync();

        await BlogArticleSeeder.SeedAsync(db, NullLogger.Instance);

        (await db.BlogArticles.CountAsync()).ShouldBe(1);
        (await db.BlogArticles.SingleAsync()).TitleEn.ShouldBe("A Real Operator's Article");
    }

    // ---- CourseSeeder --------------------------------------------------------

    private const string SampleCourseJson = """
    {
      "title": "Intro Course",
      "description": "Desc",
      "isPublished": true,
      "modules": [
        { "title": "Module 1", "sortOrder": 1,
          "lessons": [ { "title": "Lesson 1", "sortOrder": 1, "durationMinutes": 10 } ] }
      ]
    }
    """;

    [Fact]
    public async Task CourseSeeder_ValidJson_SeedsCourseWithModulesAndLessons()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var dir = TempDirWith("course.json", SampleCourseJson);

        await CourseSeeder.SeedFromJsonAsync(db, dir, NullLogger.Instance);

        var course = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons).SingleAsync();
        course.TitleEn.ShouldBe("Intro Course");
        course.Modules.Count.ShouldBe(1);
        course.Modules.Single().Lessons.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CourseSeeder_MissingDirectory_IsNoOp()
    {
        await using var db = DbContextFactory.CreateInMemory();

        await Should.NotThrowAsync(() => CourseSeeder.SeedFromJsonAsync(db, NonExistentDir(), NullLogger.Instance));

        (await db.Courses.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CourseSeeder_WhenCoursesExist_SkipsSeeding()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new ResetYourFuture.Domain.Entities.Course { Id = Guid.NewGuid(), TitleEn = "Existing" });
        await db.SaveChangesAsync();
        var dir = TempDirWith("course.json", SampleCourseJson);

        await CourseSeeder.SeedFromJsonAsync(db, dir, NullLogger.Instance);

        (await db.Courses.CountAsync()).ShouldBe(1); // unchanged
    }

    // ---- StudentSeeder -------------------------------------------------------

    [Fact]
    public async Task StudentSeeder_MissingDirectory_IsNoOp()
    {
        var um = IdentityMocks.MockUserManager();

        await Should.NotThrowAsync(() =>
            StudentSeeder.SeedFromJsonAsync(um, NonExistentDir(), "Pwd-1!", NullLogger.Instance));

        await um.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    // ---- BulkStudentSeedingService ------------------------------------------

    [Fact]
    public async Task BulkStudentSeedingService_NotDevelopment_IsNoOp()
    {
        var sp = Substitute.For<IServiceProvider>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SeedData:Enabled"] = "false" })
            .Build();
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var service = new BulkStudentSeedingService(sp, config, env, NullLogger<BulkStudentSeedingService>.Instance);

        await Should.NotThrowAsync(async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        });

        sp.DidNotReceive().GetService(Arg.Any<Type>());
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs.Where(Directory.Exists))
            Directory.Delete(d, recursive: true);
    }
}
