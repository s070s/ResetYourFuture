using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;

namespace ResetYourFuture.Infrastructure.Seeding;

/// <summary>
/// Get-or-create category resolution shared by CourseSeeder and AssessmentSeeder. The returned
/// cache must be reused across every file in a single seeding run so repeated category names
/// (e.g. "Career") don't insert duplicates before the batched SaveChangesAsync commits.
/// </summary>
internal static class CategorySeedHelper
{
    public static async Task<Dictionary<string, Category>> LoadCacheAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Categories.ToListAsync(cancellationToken);
        return existing.ToDictionary(c => c.NameEn.ToLowerInvariant(), c => c);
    }

    public static Guid? ResolveCategoryId(ApplicationDbContext db, Dictionary<string, Category> cache, string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return null;

        var key = categoryName.Trim().ToLowerInvariant();
        if (cache.TryGetValue(key, out var existing))
            return existing.Id;

        var category = new Category { Id = Guid.NewGuid(), NameEn = categoryName.Trim() };
        db.Categories.Add(category);
        cache[key] = category;
        return category.Id;
    }
}
