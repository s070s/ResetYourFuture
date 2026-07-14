using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResetYourFuture.Application.ApiServices;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;
using ResetYourFuture.TestSupport;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class AdminCategoryServiceTests
{
    private static AdminCategoryService NewService(ApplicationDbContext db) =>
        new(db, NullLogger<AdminCategoryService>.Instance);

    [Fact]
    public async Task CreateCategory_Persists()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var result = await NewService(db).CreateCategoryAsync(new SaveCategoryRequest("Career", "Καριέρα"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.NameEn.ShouldBe("Career");
        (await db.Categories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameCaseInsensitive_ReturnsConflict()
    {
        // API-3: 409, not 400 — "retry with a different name" is a conflict, not a bad request.
        await using var db = DbContextFactory.CreateInMemory();
        db.Categories.Add(new Category { Id = Guid.NewGuid(), NameEn = "Career" });
        await db.SaveChangesAsync();

        var result = await NewService(db).CreateCategoryAsync(new SaveCategoryRequest("CAREER", null));

        result.StatusCode.ShouldBe(409);
        (await db.Categories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateCategory_RenamesFields()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Old" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var result = await NewService(db).UpdateCategoryAsync(category.Id, new SaveCategoryRequest("New", "Νέο"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.NameEn.ShouldBe("New");
        result.Value.NameEl.ShouldBe("Νέο");
    }

    [Fact]
    public async Task UpdateCategory_Missing_ReturnsNotFound()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var result = await NewService(db).UpdateCategoryAsync(Guid.NewGuid(), new SaveCategoryRequest("X", null));

        result.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task UpdateCategory_DuplicateNameOfAnotherCategory_ReturnsConflict()
    {
        // API-3: 409, not 400 — "retry with a different name" is a conflict, not a bad request.
        await using var db = DbContextFactory.CreateInMemory();
        var a = new Category { Id = Guid.NewGuid(), NameEn = "Career" };
        var b = new Category { Id = Guid.NewGuid(), NameEn = "Mindset" };
        db.Categories.AddRange(a, b);
        await db.SaveChangesAsync();

        var result = await NewService(db).UpdateCategoryAsync(b.Id, new SaveCategoryRequest("career", null));

        result.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task DeleteCategory_Missing_ReturnsFalse()
    {
        await using var db = DbContextFactory.CreateInMemory();

        (await NewService(db).DeleteCategoryAsync(Guid.NewGuid())).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteCategory_SoftDeletes_AndUncategorizesReferencingContent()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var category = new Category { Id = Guid.NewGuid(), NameEn = "Career" };
        var course = new Course { Id = Guid.NewGuid(), TitleEn = "C", CategoryId = category.Id };
        var assessment = new AssessmentDefinition
        {
            Id = Guid.NewGuid(),
            Key = "k1",
            TitleEn = "A",
            SchemaJson = "{}",
            CategoryId = category.Id
        };
        db.Categories.Add(category);
        db.Courses.Add(course);
        db.AssessmentDefinitions.Add(assessment);
        await db.SaveChangesAsync();

        (await NewService(db).DeleteCategoryAsync(category.Id)).ShouldBeTrue();

        (await db.Categories.FindAsync(category.Id))!.IsDeleted.ShouldBeTrue();
        (await db.Courses.FindAsync(course.Id))!.CategoryId.ShouldBeNull();
        (await db.AssessmentDefinitions.FindAsync(assessment.Id))!.CategoryId.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveCategoryAsync_NewName_CreatesUnsavedCategory()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var id = await AdminCategoryService.ResolveCategoryAsync(db, null, "Finance");

        id.ShouldNotBeNull();
        db.Categories.Local.ShouldContain(c => c.Id == id && c.NameEn == "Finance");
    }

    [Fact]
    public async Task ResolveCategoryAsync_ExistingNameCaseInsensitive_ReusesCategory()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var existing = new Category { Id = Guid.NewGuid(), NameEn = "Finance" };
        db.Categories.Add(existing);
        await db.SaveChangesAsync();

        var id = await AdminCategoryService.ResolveCategoryAsync(db, null, "finance");

        id.ShouldBe(existing.Id);
        (await db.Categories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ResolveCategoryAsync_CategoryIdWins_WhenNoNewName()
    {
        await using var db = DbContextFactory.CreateInMemory();
        var existing = new Category { Id = Guid.NewGuid(), NameEn = "Skills" };
        db.Categories.Add(existing);
        await db.SaveChangesAsync();

        var id = await AdminCategoryService.ResolveCategoryAsync(db, existing.Id, null);

        id.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task ResolveCategoryAsync_UnknownCategoryId_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var id = await AdminCategoryService.ResolveCategoryAsync(db, Guid.NewGuid(), null);

        id.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveCategoryAsync_NoIdOrName_ReturnsNull()
    {
        await using var db = DbContextFactory.CreateInMemory();

        var id = await AdminCategoryService.ResolveCategoryAsync(db, null, null);

        id.ShouldBeNull();
    }
}
