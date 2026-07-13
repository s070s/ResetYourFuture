using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.Mappings;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Shared.Resources.Messages;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Admin CRUD operations for categories.
/// </summary>
public class AdminCategoryService(IApplicationDbContext db, ILogger<AdminCategoryService> logger) : IAdminCategoryService
{
    public async Task<PagedResult<AdminCategoryDto>> GetCategoriesAsync(
        int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        var totalCount = await db.Categories.CountAsync(cancellationToken);

        var items = await db.Categories
            .AsNoTracking()
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CategoryMappings.AdminProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCategoryDto>(items, totalCount, page, pageSize, sortBy, sortDir);
    }

    public async Task<List<CategoryOptionDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.NameEn)
            .Select(c => new CategoryOptionDto(c.Id, c.NameEn, c.NameEl))
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<AdminCategoryDto>> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.NameEn.Trim();
        if (await NameExistsAsync(name, excludeId: null, cancellationToken))
            return ServiceResult<AdminCategoryDto>.BadRequest(error: ErrorMessagesRes.CategoryNameExists);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            NameEn = name,
            NameEl = NormalizeOptional(request.NameEl)
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<AdminCategoryDto>.Created(category.ToAdminDto(0, 0));
    }

    public async Task<ServiceResult<AdminCategoryDto>> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult<AdminCategoryDto>.NotFound(error: ErrorMessagesRes.CategoryNotFound);

        var name = request.NameEn.Trim();
        if (await NameExistsAsync(name, excludeId: id, cancellationToken))
            return ServiceResult<AdminCategoryDto>.BadRequest(error: ErrorMessagesRes.CategoryNameExists);

        category.NameEn = name;
        category.NameEl = NormalizeOptional(request.NameEl);
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var courseCount = await db.Courses.CountAsync(c => c.CategoryId == id, cancellationToken);
        var assessmentCount = await db.AssessmentDefinitions.CountAsync(a => a.CategoryId == id, cancellationToken);

        return ServiceResult<AdminCategoryDto>.Ok(category.ToAdminDto(courseCount, assessmentCount));
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
            return false;

        // Explicit tracked-entity loop (not ExecuteUpdate) so this behaves identically against the
        // SQLite test provider. The DB-level OnDelete(SetNull) is a backstop for any future hard delete.
        var courses = await db.Courses.Where(c => c.CategoryId == id).ToListAsync(cancellationToken);
        foreach (var course in courses)
            course.CategoryId = null;

        var assessments = await db.AssessmentDefinitions.Where(a => a.CategoryId == id).ToListAsync(cancellationToken);
        foreach (var assessment in assessments)
            assessment.CategoryId = null;

        category.IsDeleted = true;
        category.DeletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Deleted category {CategoryId}, uncategorized {CourseCount} course(s) and {AssessmentCount} assessment(s)",
            id, courses.Count, assessments.Count);

        return true;
    }

    /// <summary>
    /// Get-or-create by case-insensitive <c>NameEn</c> match when <paramref name="newCategoryName"/>
    /// is non-blank (it wins over <paramref name="categoryId"/>), otherwise validates
    /// <paramref name="categoryId"/> exists. Newly created categories are added to the change
    /// tracker but not saved — the caller's own <c>SaveChangesAsync</c> persists both the category
    /// and the referencing course/assessment together.
    /// </summary>
    public static async Task<Guid?> ResolveCategoryAsync(
        IApplicationDbContext db, Guid? categoryId, string? newCategoryName, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(newCategoryName))
        {
            var trimmed = newCategoryName.Trim();
            var lower = trimmed.ToLower();
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.NameEn.ToLower() == lower, cancellationToken);
            if (existing is not null)
                return existing.Id;

            var category = new Category { Id = Guid.NewGuid(), NameEn = trimmed };
            db.Categories.Add(category);
            return category.Id;
        }

        if (categoryId is { } id)
        {
            var exists = await db.Categories.AnyAsync(c => c.Id == id, cancellationToken);
            return exists ? id : null;
        }

        return null;
    }

    private Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();
        return excludeId is { } id
            ? db.Categories.AnyAsync(c => c.Id != id && c.NameEn.ToLower() == lower, cancellationToken)
            : db.Categories.AnyAsync(c => c.NameEn.ToLower() == lower, cancellationToken);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
