using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Public category discovery for browse/filter chips.
/// </summary>
public class CategoryService(IApplicationDbContext db) : ICategoryService
{
    public async Task<List<CategoryDto>> GetCategoriesAsync(string scope, string lang, CancellationToken cancellationToken = default)
    {
        var isEl = Localized.IsEl(lang);
        var isAssessments = string.Equals(scope, "assessments", StringComparison.OrdinalIgnoreCase);

        var counts = isAssessments
            ? await db.AssessmentDefinitions
                .AsNoTracking()
                .Where(a => a.IsPublished && a.CategoryId != null)
                .GroupBy(a => a.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken)
            : await db.Courses
                .AsNoTracking()
                .Where(c => c.IsPublished && c.CategoryId != null)
                .GroupBy(c => c.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

        if (counts.Count == 0)
            return [];

        var countById = counts.ToDictionary(c => c.CategoryId, c => c.Count);
        var categoryIds = countById.Keys.ToList();

        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => new { c.Id, c.NameEn, c.NameEl })
            .ToListAsync(cancellationToken);

        return categories
            .Select(c => new CategoryDto(
                c.Id,
                Localized.Pick(isEl, c.NameEn, c.NameEl),
                countById[c.Id]))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
