using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

/// <summary>
/// Data-logic tests for each assistant tool against the InMemory provider, plus the
/// tool-surface contract (six tools, none for an empty identity, no user-id parameters).
/// </summary>
public class AssistantToolsTests
{
    private const string UserId = "student-1";
    private const string OtherUserId = "student-2";

    private static Course NewCourse(string titleEn, string? titleEl = null, bool published = true,
        Category? category = null, params int[] lessonsPerModule)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            TitleEn = titleEn,
            TitleEl = titleEl,
            DescriptionEn = $"About {titleEn}",
            IsPublished = published,
            Category = category
        };
        foreach (var lessonCount in lessonsPerModule)
        {
            var module = new Module { Id = Guid.NewGuid(), TitleEn = "M", CourseId = course.Id };
            for (var i = 0; i < lessonCount; i++)
                module.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TitleEn = $"L{i}" });
            course.Modules.Add(module);
        }
        return course;
    }

    [Fact]
    public void GetToolsForUser_EmptyIdentity_ReturnsNoTools()
    {
        using var db = DbContextFactory.CreateInMemory();

        new AssistantTools(db).GetToolsForUser("", "en").ShouldBeEmpty();
        new AssistantTools(db).GetToolsForUser("  ", "en").ShouldBeEmpty();
    }

    [Fact]
    public void GetToolsForUser_Authenticated_ExposesTheSixSelfServiceTools()
    {
        using var db = DbContextFactory.CreateInMemory();

        var tools = new AssistantTools(db).GetToolsForUser(UserId, "en");

        tools.Select(t => t.Name).ShouldBe([
            "get_my_enrollments", "get_my_progress", "get_my_assessment_results",
            "get_subscription_status", "search_courses", "recommend_courses"]);
    }

    [Fact]
    public async Task GetMyEnrollments_ReturnsOwnCoursesLocalized()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var mine = NewCourse("Career Start", "Καριέρα");
        var other = NewCourse("Other Course");
        db.Courses.AddRange(mine, other);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = mine.Id });
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = OtherUserId, CourseId = other.Id });
        await db.SaveChangesAsync();

        var en = await new AssistantTools(db).GetMyEnrollmentsAsync(UserId, isEl: false);
        var el = await new AssistantTools(db).GetMyEnrollmentsAsync(UserId, isEl: true);

        en.ShouldHaveSingleItem().Course.ShouldBe("Career Start");
        el.ShouldHaveSingleItem().Course.ShouldBe("Καριέρα");
    }

    [Fact]
    public async Task GetMyProgress_CountsCompletedAndTotalLessons()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = NewCourse("CV Lab", lessonsPerModule: [2, 3]); // 5 lessons total
        db.Courses.Add(course);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = course.Id });
        var lessons = course.Modules.SelectMany(m => m.Lessons).Take(2).ToList();
        foreach (var lesson in lessons)
            db.LessonCompletions.Add(new LessonCompletion { Id = Guid.NewGuid(), UserId = UserId, LessonId = lesson.Id });
        // Another user's completion must not count.
        db.LessonCompletions.Add(new LessonCompletion
        {
            Id = Guid.NewGuid(),
            UserId = OtherUserId,
            LessonId = course.Modules.SelectMany(m => m.Lessons).Last().Id
        });
        await db.SaveChangesAsync();

        var progress = await new AssistantTools(db).GetMyProgressAsync(UserId, isEl: false);

        var row = progress.ShouldHaveSingleItem();
        row.CompletedLessons.ShouldBe(2);
        row.TotalLessons.ShouldBe(5);
    }

    [Fact]
    public async Task GetMyAssessmentResults_ReturnsRecentTitlesOnly()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var definition = new AssessmentDefinition { Id = Guid.NewGuid(), Key = "k", TitleEn = "Mindset Check", SchemaJson = "{}" };
        db.AssessmentDefinitions.Add(definition);
        for (var i = 0; i < 7; i++)
            db.AssessmentSubmissions.Add(new AssessmentSubmission
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                AssessmentDefinitionId = definition.Id,
                AnswersJson = "{\"secret\":\"answer\"}",
                SubmittedAt = new DateTimeOffset(2024, 1, i + 1, 0, 0, 0, TimeSpan.Zero)
            });
        await db.SaveChangesAsync();

        var results = await new AssistantTools(db).GetMyAssessmentResultsAsync(UserId, isEl: false);

        results.Count.ShouldBe(5); // capped, most recent first
        results[0].SubmittedOn.ShouldBe("2024-01-07");
        results.ShouldAllBe(r => r.Assessment == "Mindset Check");
    }

    [Fact]
    public async Task GetSubscriptionStatus_ActiveSubscription_ReportsPlan_OtherwiseFree()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Pro Monthly", Tier = SubscriptionTier.Pro };
        db.SubscriptionPlans.Add(plan);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SubscriptionPlanId = plan.Id,
            IsActive = true,
            ExpiresAt = new DateTime(2026, 12, 31)
        });
        await db.SaveChangesAsync();

        var subscribed = await new AssistantTools(db).GetSubscriptionStatusAsync(UserId);
        var free = await new AssistantTools(db).GetSubscriptionStatusAsync(OtherUserId);

        subscribed.Plan.ShouldBe("Pro Monthly");
        subscribed.Tier.ShouldBe("Pro");
        subscribed.ExpiresOn.ShouldBe("2026-12-31");
        free.Plan.ShouldBe("Free");
        free.ExpiresOn.ShouldBeNull();
    }

    [Fact]
    public async Task SearchCourses_MatchesPublishedOnly_CapsResults()
    {
        await using var db = DbContextFactory.CreateInMemory();
        for (var i = 0; i < 8; i++)
            db.Courses.Add(NewCourse($"Interview Skills {i}"));
        db.Courses.Add(NewCourse("Interview Draft", published: false));
        db.Courses.Add(NewCourse("Unrelated"));
        await db.SaveChangesAsync();

        var hits = await new AssistantTools(db).SearchCoursesAsync("Interview", maxResults: 99, isEl: false);

        hits.Count.ShouldBe(5); // clamped to the cap
        hits.ShouldAllBe(h => h.Title.StartsWith("Interview Skills"));
    }

    [Fact]
    public async Task RecommendCourses_PrefersOwnCategories_ExcludesEnrolled_FallsBackToAnyPublished()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var careers = new Category { Id = Guid.NewGuid(), NameEn = "Careers" };
        var enrolled = NewCourse("Career Start", category: careers);
        var sameCategory = NewCourse("Career Advanced", category: careers);
        var otherCategory = NewCourse("Finance Basics", category: new Category { Id = Guid.NewGuid(), NameEn = "Finance" });
        db.Courses.AddRange(enrolled, sameCategory, otherCategory);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = UserId, CourseId = enrolled.Id });
        await db.SaveChangesAsync();

        var forMe = await new AssistantTools(db).RecommendCoursesAsync(UserId, isEl: false);
        var forNewUser = await new AssistantTools(db).RecommendCoursesAsync(OtherUserId, isEl: false);

        forMe.ShouldHaveSingleItem().Title.ShouldBe("Career Advanced"); // same category, not enrolled
        forNewUser.Select(c => c.Title).ShouldBe(["Career Advanced", "Career Start", "Finance Basics"]);
    }
}
