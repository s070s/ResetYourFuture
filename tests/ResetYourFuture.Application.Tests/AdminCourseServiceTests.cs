using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.TestSupport;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AdminCourseServiceTests
{
    private const string Admin = "admin-1";

    private static AdminCourseService NewService(ApplicationDbContext db) =>
        new(db, NullLogger<AdminCourseService>.Instance, new HtmlSanitizer());

    private static SaveCourseRequest Request(
        string titleEn = "Title", string? descEn = null, SubscriptionTier tier = SubscriptionTier.Free,
        Guid? categoryId = null, string? newCategoryName = null) =>
        new(titleEn, null, descEn, null, tier, categoryId, newCategoryName);

    [Fact]
    public async Task GetCourseById_Missing_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).GetCourseByIdAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task GetCourseById_ReturnsCountsForModulesLessonsEnrollments()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        var module = new Module { Id = Guid.NewGuid(), TitleEn = "M", CourseId = course.Id };
        module.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TitleEn = "L1" });
        module.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TitleEn = "L2" });
        course.Modules.Add(module);
        db.Courses.Add(course);
        db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), UserId = "u", CourseId = course.Id });
        await db.SaveChangesAsync();

        var dto = await NewService(db).GetCourseByIdAsync(course.Id);

        dto!.ModuleCount.ShouldBe(1);
        dto.TotalLessons.ShouldBe(2);
        dto.EnrollmentCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetCourses_OrdersByCreatedAtDescending()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var older = new Course { Id = Guid.NewGuid(), TitleEn = "Older" };
        var newer = new Course { Id = Guid.NewGuid(), TitleEn = "Newer" };
        db.Courses.AddRange(older, newer);
        await db.SaveChangesAsync();
        // Audit stamping overwrites CreatedAt on insert; reassign on a 2nd (Modified) save to get distinct values.
        older.CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        newer.CreatedAt = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        var page = await NewService(db).GetCoursesAsync(1, 10);

        page.Items.Select(i => i.TitleEn).ShouldBe(new[] { "Newer", "Older" });
    }

    [Fact]
    public async Task CreateCourse_PersistsUnpublishedWithZeroCounts()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateCourseAsync(Request("New Course"), Admin);

        dto.IsPublished.ShouldBeFalse();
        dto.ModuleCount.ShouldBe(0);
        dto.EnrollmentCount.ShouldBe(0);
        (await db.Courses.FindAsync(dto.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateCourse_SanitizesDescriptionHtml()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db)
            .CreateCourseAsync(Request(descEn: "<script>alert(1)</script><p>Hello</p>"), Admin);

        dto.DescriptionEn!.ShouldNotContain("<script");
        dto.DescriptionEn!.ShouldContain("Hello");
    }

    [Fact]
    public async Task CreateCourse_NullDescription_StaysNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateCourseAsync(Request(descEn: null), Admin);

        dto.DescriptionEn.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateCourse_Missing_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).UpdateCourseAsync(Guid.NewGuid(), Request(), Admin)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateCourse_UpdatesFieldsAndSanitizes()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "Old" };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var dto = await NewService(db).UpdateCourseAsync(
            course.Id, Request("Updated", descEn: "<b onclick=\"x\">bold</b>", tier: SubscriptionTier.Pro), Admin);

        dto!.TitleEn.ShouldBe("Updated");
        dto.RequiredTier.ShouldBe(SubscriptionTier.Pro);
        dto.DescriptionEn!.ShouldNotContain("onclick");
    }

    [Fact]
    public async Task DeleteCourse_Missing_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).DeleteCourseAsync(Guid.NewGuid(), Admin)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteCourse_SoftDeletes_HiddenFromSubsequentQueries()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C" };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        (await svc.DeleteCourseAsync(course.Id, Admin)).ShouldBeTrue();

        (await svc.GetCourseByIdAsync(course.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task PublishCourse_SetsPublished()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C", IsPublished = false };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        (await svc.PublishCourseAsync(course.Id, Admin)).ShouldBeTrue();
        (await svc.GetCourseByIdAsync(course.Id))!.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task UnpublishCourse_ClearsPublished()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C", IsPublished = true };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        (await svc.UnpublishCourseAsync(course.Id, Admin)).ShouldBeTrue();
        (await svc.GetCourseByIdAsync(course.Id))!.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishCourse_Missing_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).PublishCourseAsync(Guid.NewGuid(), Admin)).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateCourse_WithExistingCategoryId_AssignsCategory()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Career" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var dto = await NewService(db).CreateCourseAsync(Request(categoryId: category.Id), Admin);

        dto.CategoryId.ShouldBe(category.Id);
        dto.CategoryNameEn.ShouldBe("Career");
    }

    [Fact]
    public async Task CreateCourse_WithNewCategoryName_CreatesAndAssignsCategory()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateCourseAsync(Request(newCategoryName: "Finance"), Admin);

        dto.CategoryId.ShouldNotBeNull();
        dto.CategoryNameEn.ShouldBe("Finance");
        (await db.Categories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateCourse_NewCategoryName_ReusesExistingByCaseInsensitiveMatch()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var existing = new Category { Id = Guid.NewGuid(), NameEn = "Finance" };
        db.Categories.Add(existing);
        await db.SaveChangesAsync();

        var dto = await NewService(db).CreateCourseAsync(Request(newCategoryName: "finance"), Admin);

        dto.CategoryId.ShouldBe(existing.Id);
        (await db.Categories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateCourse_NoCategory_LeavesCategoryNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var dto = await NewService(db).CreateCourseAsync(Request(), Admin);

        dto.CategoryId.ShouldBeNull();
        dto.CategoryNameEn.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateCourse_ChangesCategory()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var oldCategory = new Category { Id = Guid.NewGuid(), NameEn = "Old" };
        var newCategory = new Category { Id = Guid.NewGuid(), NameEn = "New" };
        db.Categories.AddRange(oldCategory, newCategory);
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C", CategoryId = oldCategory.Id };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var dto = await NewService(db).UpdateCourseAsync(course.Id, Request(categoryId: newCategory.Id), Admin);

        dto!.CategoryId.ShouldBe(newCategory.Id);
    }
}
