using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;

namespace ResetYourFuture.Application.ApiServices;

/// <inheritdoc cref="ILearningPathService"/>
public class LearningPathService(IApplicationDbContext db, ILogger<LearningPathService> logger) : ILearningPathService
{
    public async Task<IReadOnlyList<LearningPathListItemDto>> GetPublishedAsync(
        string? userId, string lang, CancellationToken cancellationToken = default)
    {
        var isEl = Localized.IsEl(lang);

        var rows = await db.LearningPaths
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.TitleEn,
                p.TitleEl,
                p.DescriptionEn,
                p.DescriptionEl,
                p.CategoryId,
                CategoryNameEn = p.Category != null ? p.Category.NameEn : null,
                CategoryNameEl = p.Category != null ? p.Category.NameEl : null,
                StepCount = p.Steps.Count,
                CompletedSteps = userId == null
                    ? 0
                    : p.Steps.Count(s => db.Enrollments.Any(e =>
                        e.CourseId == s.CourseId && e.UserId == userId && e.Status == EnrollmentStatus.Completed))
            })
            .ToListAsync(cancellationToken);

        return rows.Select(p => new LearningPathListItemDto(
            p.Id,
            Localized.Pick(isEl, p.TitleEn, p.TitleEl),
            Localized.Pick(isEl, p.DescriptionEn, p.DescriptionEl),
            p.CategoryId,
            Localized.Pick(isEl, p.CategoryNameEn, p.CategoryNameEl),
            p.StepCount,
            p.CompletedSteps)).ToList();
    }

    public async Task<LearningPathDetailDto?> GetByIdAsync(
        Guid id, string? userId, string lang, CancellationToken cancellationToken = default)
    {
        var isEl = Localized.IsEl(lang);

        var path = await db.LearningPaths
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsPublished)
            .Select(p => new
            {
                p.Id,
                p.TitleEn,
                p.TitleEl,
                p.DescriptionEn,
                p.DescriptionEl,
                p.CategoryId,
                CategoryNameEn = p.Category != null ? p.Category.NameEn : null,
                CategoryNameEl = p.Category != null ? p.Category.NameEl : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (path is null)
            return null;

        var steps = await db.LearningPathSteps
            .AsNoTracking()
            .Where(s => s.LearningPathId == id)
            .OrderBy(s => s.StepOrder)
            .Select(s => new
            {
                s.CourseId,
                s.StepOrder,
                CourseTitleEn = s.Course!.TitleEn,
                CourseTitleEl = s.Course.TitleEl
            })
            .ToListAsync(cancellationToken);

        HashSet<Guid> completedCourseIds;
        if (userId is null)
        {
            completedCourseIds = [];
        }
        else
        {
            var courseIds = steps.Select(s => s.CourseId).ToList();
            completedCourseIds = await db.Enrollments
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.Status == EnrollmentStatus.Completed && courseIds.Contains(e.CourseId))
                .Select(e => e.CourseId)
                .ToHashSetAsync(cancellationToken);
        }

        // Locked/next are only meaningful for a signed-in student; anonymous visitors get a
        // browsing-only view (every step unlocked, nothing marked "next").
        var stepDtos = new List<LearningPathStepDto>();
        var previousCompleted = true;
        var nextAssigned = false;

        foreach (var s in steps)
        {
            var isCompleted = userId is not null && completedCourseIds.Contains(s.CourseId);
            var isLocked = userId is not null && !previousCompleted;
            var isNext = userId is not null && !isCompleted && !isLocked && !nextAssigned;
            if (isNext)
                nextAssigned = true;

            stepDtos.Add(new LearningPathStepDto(
                s.CourseId,
                Localized.Pick(isEl, s.CourseTitleEn, s.CourseTitleEl),
                s.StepOrder,
                isCompleted,
                isLocked,
                isNext));

            previousCompleted = isCompleted;
        }

        return new LearningPathDetailDto(
            path.Id,
            Localized.Pick(isEl, path.TitleEn, path.TitleEl),
            Localized.Pick(isEl, path.DescriptionEn, path.DescriptionEl),
            path.CategoryId,
            Localized.Pick(isEl, path.CategoryNameEn, path.CategoryNameEl),
            stepDtos);
    }

    public async Task<PagedResult<AdminLearningPathDto>> GetAllForAdminAsync(
        int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = PagingParams.Normalize(page, pageSize);

        var query = db.LearningPaths.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminLearningPathDto(
                p.Id, p.TitleEn, p.TitleEl, p.CategoryId,
                p.Category != null ? p.Category.NameEn : null,
                p.DisplayOrder, p.IsPublished, p.Steps.Count,
                p.CreatedAt, p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminLearningPathDto>(items, totalCount, page, pageSize, sortBy, sortDir);
    }

    public async Task<AdminLearningPathDetailDto?> GetAdminByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = await db.LearningPaths
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (path is null)
            return null;

        var steps = await db.LearningPathSteps
            .AsNoTracking()
            .Where(s => s.LearningPathId == id)
            .OrderBy(s => s.StepOrder)
            .Select(s => new AdminLearningPathStepDto(s.Id, s.CourseId, s.Course!.TitleEn, s.StepOrder))
            .ToListAsync(cancellationToken);

        return ToAdminDetailDto(path, steps);
    }

    public async Task<AdminLearningPathDetailDto> CreateAsync(SaveLearningPathRequest request, CancellationToken cancellationToken = default)
    {
        var path = new LearningPath
        {
            Id = Guid.NewGuid(),
            TitleEn = request.TitleEn,
            TitleEl = request.TitleEl,
            DescriptionEn = request.DescriptionEn,
            DescriptionEl = request.DescriptionEl,
            CategoryId = request.CategoryId,
            DisplayOrder = request.DisplayOrder
        };

        db.LearningPaths.Add(path);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Learning path created: {Id} '{Title}'.", path.Id, path.TitleEn);
        return await GetAdminByIdAsync(path.Id, cancellationToken) ?? ToAdminDetailDto(path, []);
    }

    public async Task<AdminLearningPathDetailDto?> UpdateAsync(Guid id, SaveLearningPathRequest request, CancellationToken cancellationToken = default)
    {
        var path = await db.LearningPaths.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (path is null)
            return null;

        path.TitleEn = request.TitleEn;
        path.TitleEl = request.TitleEl;
        path.DescriptionEn = request.DescriptionEn;
        path.DescriptionEl = request.DescriptionEl;
        path.CategoryId = request.CategoryId;
        path.DisplayOrder = request.DisplayOrder;
        path.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return await GetAdminByIdAsync(id, cancellationToken);
    }

    public async Task<bool> PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = await db.LearningPaths.FindAsync([id], cancellationToken);
        if (path is null)
            return false;

        if (!path.IsPublished)
        {
            path.IsPublished = true;
            path.PublishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = await db.LearningPaths.FindAsync([id], cancellationToken);
        if (path is null)
            return false;

        if (path.IsPublished)
        {
            path.IsPublished = false;
            path.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = await db.LearningPaths.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (path is null)
            return false;

        path.IsDeleted = true;
        path.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Learning path deleted: {Id}.", id);
        return true;
    }

    public async Task<ServiceResult<AdminLearningPathDetailDto>> AddStepAsync(
        Guid pathId, Guid courseId, CancellationToken cancellationToken = default)
    {
        var pathExists = await db.LearningPaths.AnyAsync(p => p.Id == pathId, cancellationToken);
        if (!pathExists)
            return ServiceResult<AdminLearningPathDetailDto>.NotFound(error: "Learning path not found.");

        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId, cancellationToken);
        if (!courseExists)
            return ServiceResult<AdminLearningPathDetailDto>.BadRequest(error: "Course not found.");

        var alreadyInPath = await db.LearningPathSteps
            .AnyAsync(s => s.LearningPathId == pathId && s.CourseId == courseId, cancellationToken);
        if (alreadyInPath)
            return ServiceResult<AdminLearningPathDetailDto>.Conflict(error: "This course is already a step in the path.");

        var maxOrder = await db.LearningPathSteps
            .Where(s => s.LearningPathId == pathId)
            .Select(s => (int?)s.StepOrder)
            .MaxAsync(cancellationToken) ?? 0;

        db.LearningPathSteps.Add(new LearningPathStep
        {
            LearningPathId = pathId,
            CourseId = courseId,
            StepOrder = maxOrder + 1
        });
        await db.SaveChangesAsync(cancellationToken);

        var detail = await GetAdminByIdAsync(pathId, cancellationToken);
        return detail is null
            ? ServiceResult<AdminLearningPathDetailDto>.NotFound(error: "Learning path not found.")
            : ServiceResult<AdminLearningPathDetailDto>.Ok(detail);
    }

    public async Task<bool> RemoveStepAsync(Guid pathId, Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = await db.LearningPathSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.LearningPathId == pathId, cancellationToken);
        if (step is null)
            return false;

        var removedOrder = step.StepOrder;
        db.LearningPathSteps.Remove(step);
        await db.SaveChangesAsync(cancellationToken);

        // Close the gap left in StepOrder so the sequence stays contiguous 1..N. Ascending order
        // means each row shifts into the slot the previous row (or the delete) just vacated, so
        // no two rows can transiently collide on the unique (LearningPathId, StepOrder) index.
        var following = await db.LearningPathSteps
            .Where(s => s.LearningPathId == pathId && s.StepOrder > removedOrder)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);

        foreach (var s in following)
            s.StepOrder--;

        if (following.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> MoveStepUpAsync(Guid pathId, Guid stepId, CancellationToken cancellationToken = default)
    {
        var current = await db.LearningPathSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.LearningPathId == pathId, cancellationToken);
        if (current is null)
            return false;

        var previous = await db.LearningPathSteps
            .Where(s => s.LearningPathId == pathId && s.StepOrder < current.StepOrder)
            .OrderByDescending(s => s.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is null)
            return false;

        await SwapStepOrderAsync(current, previous, cancellationToken);
        return true;
    }

    public async Task<bool> MoveStepDownAsync(Guid pathId, Guid stepId, CancellationToken cancellationToken = default)
    {
        var current = await db.LearningPathSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.LearningPathId == pathId, cancellationToken);
        if (current is null)
            return false;

        var next = await db.LearningPathSteps
            .Where(s => s.LearningPathId == pathId && s.StepOrder > current.StepOrder)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (next is null)
            return false;

        await SwapStepOrderAsync(current, next, cancellationToken);
        return true;
    }

    /// <summary>
    /// Swaps two steps' StepOrder via a temporary sentinel. The unique (LearningPathId,
    /// StepOrder) index is checked per-statement (not deferred), so a direct A→B, B→A swap in
    /// one save would transiently give two rows the same StepOrder and fail.
    /// </summary>
    private async Task SwapStepOrderAsync(LearningPathStep a, LearningPathStep b, CancellationToken cancellationToken)
    {
        var (orderA, orderB) = (a.StepOrder, b.StepOrder);

        a.StepOrder = -1;
        await db.SaveChangesAsync(cancellationToken);

        b.StepOrder = orderA;
        a.StepOrder = orderB;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AdminLearningPathDetailDto ToAdminDetailDto(LearningPath path, IReadOnlyList<AdminLearningPathStepDto> steps) => new(
        path.Id, path.TitleEn, path.TitleEl, path.DescriptionEn, path.DescriptionEl,
        path.CategoryId, path.Category?.NameEn, path.DisplayOrder, path.IsPublished, steps);
}
