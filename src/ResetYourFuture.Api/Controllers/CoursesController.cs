using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Domain.Enums;
using ResetYourFuture.Shared.Courses;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// API endpoints for course discovery, enrollment, and consumption.
/// All endpoints require authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(ApplicationDbContext db, ILogger<CoursesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    // Helper to determine content type from lesson fields
    private static int GetContentType(Lesson lesson)
    {
        // Priority: Video > PDF > Text
        if (!string.IsNullOrEmpty(lesson.VideoPath)) return 2; // Video
        if (!string.IsNullOrEmpty(lesson.PdfPath)) return 3;   // PDF (new type)
        return 1; // Text (default)
    }

    /// <summary>
    /// Get list of all published courses with enrollment status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CourseListItemDto>>> GetCourses()
    {
        var userId = UserId;

        var courses = await _db.Courses
            .Where(c => c.IsPublished)
            .Select(c => new CourseListItemDto(
                c.Id,
                c.Title,
                c.Description,
                c.Enrollments.Any(e => e.UserId == userId),
                c.Modules.SelectMany(m => m.Lessons).Count()
            ))
            .ToListAsync();

        return Ok(courses);
    }

    /// <summary>
    /// Get full course detail including modules, lessons, and progress.
    /// </summary>
    [HttpGet("{courseId:guid}")]
    public async Task<ActionResult<CourseDetailDto>> GetCourse(Guid courseId)
    {
        var userId = UserId;

        var course = await _db.Courses
            .Include(c => c.Modules.OrderBy(m => m.SortOrder))
                .ThenInclude(m => m.Lessons.OrderBy(l => l.SortOrder))
            .Include(c => c.Enrollments.Where(e => e.UserId == userId))
            .FirstOrDefaultAsync(c => c.Id == courseId && c.IsPublished);

        if (course is null)
            return NotFound("Course not found");

        var enrollment = course.Enrollments.FirstOrDefault();
        var allLessonIds = course.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).ToList();
        var completedLessonIds = await _db.LessonCompletions
            .Where(lc => lc.UserId == userId && allLessonIds.Contains(lc.LessonId))
            .Select(lc => lc.LessonId)
            .ToHashSetAsync();

        var totalLessons = allLessonIds.Count;
        var completedLessons = completedLessonIds.Count;
        var progressPercent = totalLessons > 0 ? Math.Round((double)completedLessons / totalLessons * 100, 1) : 0;

        var dto = new CourseDetailDto(
            course.Id,
            course.Title,
            course.Description,
            enrollment is not null,
            enrollment?.Status == EnrollmentStatus.Completed,
            completedLessons,
            totalLessons,
            progressPercent,
            course.Modules.Select(m => new ModuleDto(
                m.Id,
                m.Title,
                m.Description,
                m.SortOrder,
                m.Lessons.Select(l => new LessonSummaryDto(
                    l.Id,
                    l.Title,
                    GetContentType(l),
                    l.DurationMinutes,
                    l.SortOrder,
                    completedLessonIds.Contains(l.Id)
                )).ToList()
            )).ToList()
        );

        return Ok(dto);
    }

    /// <summary>
    /// Enroll the current user in a course.
    /// </summary>
    [HttpPost("{courseId:guid}/enroll")]
    public async Task<ActionResult<EnrollmentResultDto>> Enroll(Guid courseId)
    {
        var userId = UserId;

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.IsPublished);
        if (course is null)
            return NotFound(new EnrollmentResultDto(false, "Course not found", null));

        var existing = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

        if (existing is not null)
        {
            return Ok(new EnrollmentResultDto(true, "Already enrolled", existing.Id));
        }

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = courseId,
            EnrolledAt = DateTime.UtcNow,
            Status = EnrollmentStatus.Active
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} enrolled in course {CourseId}", userId, courseId);

        return Ok(new EnrollmentResultDto(true, "Enrolled successfully", enrollment.Id));
    }

    /// <summary>
    /// Get full lesson detail for the lesson viewer.
    /// </summary>
    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<ActionResult<LessonDetailDto>> GetLesson(Guid lessonId)
    {
        var userId = UserId;

        var lesson = await _db.Lessons
            .Include(l => l.Module)
                .ThenInclude(m => m.Course)
            .Include(l => l.Module)
                .ThenInclude(m => m.Lessons.OrderBy(x => x.SortOrder))
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return NotFound("Lesson not found");

        var course = lesson.Module.Course;
        if (!course.IsPublished)
            return NotFound("Course not found");

        // Check enrollment
        var isEnrolled = await _db.Enrollments
            .AnyAsync(e => e.UserId == userId && e.CourseId == course.Id);

        if (!isEnrolled)
            return BadRequest("You must be enrolled in this course to view lessons");

        // Check completion
        var isCompleted = await _db.LessonCompletions
            .AnyAsync(lc => lc.UserId == userId && lc.LessonId == lessonId);

        // Get all lessons in course for navigation
        var allLessons = await _db.Lessons
            .Where(l => l.Module.CourseId == course.Id)
            .OrderBy(l => l.Module.SortOrder)
            .ThenBy(l => l.SortOrder)
            .Select(l => l.Id)
            .ToListAsync();

        var currentIndex = allLessons.IndexOf(lessonId);
        var previousLessonId = currentIndex > 0 ? allLessons[currentIndex - 1] : (Guid?)null;
        var nextLessonId = currentIndex < allLessons.Count - 1 ? allLessons[currentIndex + 1] : (Guid?)null;

        var dto = new LessonDetailDto(
            lesson.Id,
            lesson.Title,
            GetContentType(lesson),
            lesson.Content,
            lesson.DurationMinutes,
            isCompleted,
            lesson.ModuleId,
            lesson.Module.Title,
            course.Id,
            course.Title,
            previousLessonId,
            nextLessonId
        );

        return Ok(dto);
    }

    /// <summary>
    /// Mark a lesson as completed.
    /// </summary>
    [HttpPost("lessons/{lessonId:guid}/complete")]
    public async Task<ActionResult<LessonCompletionResultDto>> CompleteLesson(Guid lessonId)
    {
        var userId = UserId;

        var lesson = await _db.Lessons
            .Include(l => l.Module)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return NotFound(new LessonCompletionResultDto(false, "Lesson not found", 0, 0, 0, false));

        var courseId = lesson.Module.CourseId;

        // Check enrollment
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

        if (enrollment is null)
            return BadRequest(new LessonCompletionResultDto(false, "Not enrolled in this course", 0, 0, 0, false));

        // Check if already completed
        var existingCompletion = await _db.LessonCompletions
            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.LessonId == lessonId);

        if (existingCompletion is null)
        {
            var completion = new LessonCompletion
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessonId,
                CompletedAt = DateTime.UtcNow
            };
            _db.LessonCompletions.Add(completion);
        }

        // Calculate progress
        var allLessonIds = await _db.Lessons
            .Where(l => l.Module.CourseId == courseId)
            .Select(l => l.Id)
            .ToListAsync();

        var completedCount = await _db.LessonCompletions
            .CountAsync(lc => lc.UserId == userId && allLessonIds.Contains(lc.LessonId));

        // Include current lesson if just completed
        if (existingCompletion is null)
            completedCount++;

        var totalLessons = allLessonIds.Count;
        var progressPercent = totalLessons > 0 ? Math.Round((double)completedCount / totalLessons * 100, 1) : 0;
        var courseCompleted = completedCount >= totalLessons;

        // Update enrollment if course completed
        if (courseCompleted && enrollment.Status != EnrollmentStatus.Completed)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("User {UserId} completed course {CourseId}", userId, courseId);
        }

        await _db.SaveChangesAsync();

        return Ok(new LessonCompletionResultDto(
            true,
            existingCompletion is null ? "Lesson completed" : "Already completed",
            completedCount,
            totalLessons,
            progressPercent,
            courseCompleted
        ));
    }
}
