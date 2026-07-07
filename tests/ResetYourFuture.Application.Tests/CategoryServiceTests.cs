using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class CategoryServiceTests
{
    private static CategoryService NewService(ApplicationDbContext db) => new(db);

    [Fact]
    public async Task GetCategories_CoursesScope_OnlyCountsPublishedCourses()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Career" };
        db.Categories.Add(category);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = true, CategoryId = category.Id });
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C2", IsPublished = false, CategoryId = category.Id });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("courses", "en");

        var dto = result.ShouldHaveSingleItem();
        dto.Name.ShouldBe("Career");
        dto.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetCategories_AssessmentsScope_CountsOnlyAssessments()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Career" };
        db.Categories.Add(category);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = true, CategoryId = category.Id });
        db.AssessmentDefinitions.Add(new AssessmentDefinition
        {
            Id = Guid.NewGuid(), Key = "k1", TitleEn = "A1", SchemaJson = "{}", IsPublished = true, CategoryId = category.Id
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("assessments", "en");

        var dto = result.ShouldHaveSingleItem();
        dto.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetCategories_CategoryWithNoPublishedItems_IsExcluded()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Empty" };
        db.Categories.Add(category);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = false, CategoryId = category.Id });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("courses", "en");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategories_UncategorizedPublishedItems_DoNotProduceEntry()
    {
        await using var db = DbContextFactory.CreateInMemory();
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = true, CategoryId = null });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("courses", "en");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategories_GreekFallsBackToEnglishWhenNameElNull()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Career", NameEl = null };
        db.Categories.Add(category);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = true, CategoryId = category.Id });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("courses", "el");

        result.ShouldHaveSingleItem().Name.ShouldBe("Career");
    }

    [Fact]
    public async Task GetCategories_GreekUsesNameElWhenPresent()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Career", NameEl = "Καριέρα" };
        db.Categories.Add(category);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = true, CategoryId = category.Id });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("courses", "el");

        result.ShouldHaveSingleItem().Name.ShouldBe("Καριέρα");
    }

    [Fact]
    public async Task GetCategories_OrderedByName()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var zeta = new Category { Id = Guid.NewGuid(), NameEn = "Zeta" };
        var alpha = new Category { Id = Guid.NewGuid(), NameEn = "Alpha" };
        db.Categories.AddRange(zeta, alpha);
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C1", IsPublished = true, CategoryId = zeta.Id });
        db.Courses.Add(new Course { Id = Guid.NewGuid(), TitleEn = "C2", IsPublished = true, CategoryId = alpha.Id });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCategoriesAsync("courses", "en");

        result.Select(c => c.Name).ShouldBe(new[] { "Alpha", "Zeta" });
    }
}
