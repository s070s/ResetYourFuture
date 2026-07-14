using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Extensions;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Admin CRUD operations for courses.
/// </summary>
public class AdminCourseService(
    IApplicationDbContext db,
    ILogger<AdminCourseService> logger,
    IHtmlSanitizer sanitizer) : IAdminCourseService
{
    public async Task<AdminCourseDto?> GetCourseByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses
            .AsNoTracking()
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return course is null ? null : MapToDto(course);
    }

    public async Task<PagedResult<AdminCourseDto>> GetCoursesAsync(
        int page, int pageSize, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        var totalCount = await db.Courses.CountAsync(cancellationToken);

        var items = await db.Courses
            .AsNoTracking()
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCourseDto(
                c.Id,
                c.TitleEn,
                c.TitleEl,
                c.DescriptionEn,
                c.DescriptionEl,
                c.IsPublished,
                c.CreatedAt,
                c.UpdatedAt,
                c.Modules.Count,
                c.Modules.SelectMany(m => m.Lessons).Count(),
                c.Enrollments.Count,
                c.RequiredTier,
                c.CategoryId,
                c.Category != null ? c.Category.NameEn : null
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCourseDto>(items, totalCount, page, pageSize, sortBy, sortDir);
    }

    public async Task<AdminCourseDto> CreateCourseAsync(SaveCourseRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var categoryId = await AdminCategoryService.ResolveCategoryAsync(db, request.CategoryId, request.NewCategoryName, cancellationToken);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            TitleEn = request.TitleEn,
            TitleEl = request.TitleEl,
            DescriptionEn = request.DescriptionEn is not null ? sanitizer.Sanitize(request.DescriptionEn) : null,
            DescriptionEl = request.DescriptionEl is not null ? sanitizer.Sanitize(request.DescriptionEl) : null,
            RequiredTier = request.RequiredTier,
            CategoryId = categoryId,
            IsPublished = false,
            UpdatedByUserId = userId
        };

        db.Courses.Add(course);
        await db.SaveChangesAsync(cancellationToken);

        return await GetCourseByIdAsync(course.Id, cancellationToken) ?? MapToDto(course);
    }

    public async Task<AdminCourseDto?> UpdateCourseAsync(Guid id, SaveCourseRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course is null)
            return null;

        var categoryId = await AdminCategoryService.ResolveCategoryAsync(db, request.CategoryId, request.NewCategoryName, cancellationToken);

        course.TitleEn = request.TitleEn;
        course.TitleEl = request.TitleEl;
        course.DescriptionEn = request.DescriptionEn is not null ? sanitizer.Sanitize(request.DescriptionEn) : null;
        course.DescriptionEl = request.DescriptionEl is not null ? sanitizer.Sanitize(request.DescriptionEl) : null;
        course.RequiredTier = request.RequiredTier;
        course.CategoryId = categoryId;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        course.UpdatedByUserId = userId;

        await db.SaveChangesAsync(cancellationToken);

        return await GetCourseByIdAsync(course.Id, cancellationToken) ?? MapToDto(course);
    }

    public async Task<bool> DeleteCourseAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses
            .Include(c => c.Enrollments)
            .Include(c => c.Modules)
            .ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course is null)
            return false;

        // Soft-delete the course so students retain access to their earned certificates.
        // The global IsDeleted query filter will hide the course and its enrollments
        // from all normal queries while leaving certificate records intact.
        var now = DateTimeOffset.UtcNow;
        course.IsDeleted = true;
        course.DeletedAt = now;
        course.UpdatedByUserId = userId;

        // DB-4: soft-deleting a course previously left its Modules/Lessons with IsDeleted=false —
        // still live rows, still readable/editable via the admin module/lesson endpoints directly
        // by id, even though the parent course was gone from every course-scoped view. Cascade
        // the same stamp to every module and lesson so "deleted" means the same thing everywhere.
        var moduleCount = 0;
        var lessonCount = 0;
        foreach (var module in course.Modules)
        {
            module.IsDeleted = true;
            module.DeletedAt = now;
            module.UpdatedByUserId = userId;
            moduleCount++;

            foreach (var lesson in module.Lessons)
            {
                lesson.IsDeleted = true;
                lesson.DeletedAt = now;
                lesson.UpdatedByUserId = userId;
                lessonCount++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Admin {UserId} soft-deleted course {CourseId} with {Enrollments} enrollment(s), {Modules} module(s), {Lessons} lesson(s)",
            userId, id, course.Enrollments.Count, moduleCount, lessonCount);

        return true;
    }

    public async Task<bool> PublishCourseAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses.FindAsync([id], cancellationToken);
        if (course is null)
            return false;

        if (!course.IsPublished)
        {
            course.IsPublished = true;
            course.PublishedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = userId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> UnpublishCourseAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses.FindAsync([id], cancellationToken);
        if (course is null)
            return false;

        if (course.IsPublished)
        {
            course.IsPublished = false;
            course.UpdatedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = userId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static AdminCourseDto MapToDto(Course course) => new(
        course.Id,
        course.TitleEn,
        course.TitleEl,
        course.DescriptionEn,
        course.DescriptionEl,
        course.IsPublished,
        course.CreatedAt,
        course.UpdatedAt,
        course.Modules.Count,
        course.Modules.SelectMany(m => m.Lessons).Count(),
        course.Enrollments.Count,
        course.RequiredTier,
        course.CategoryId,
        course.Category?.NameEn
    );
}
