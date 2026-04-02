using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Domain.Enums;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Handles course discovery, enrollment, and lesson consumption for students.
/// </summary>
public class CourseService(
    ApplicationDbContext db ,
    ISubscriptionService subscriptionService ,
    ICertificateService certificateService ,
    ILogger<CourseService> logger ) : ICourseService
{
    public async Task<PagedResult<CourseListItemDto>> GetPublishedCoursesAsync(
        string userId , int page , int pageSize , string lang )
    {
        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var query = db.Courses
            .AsNoTracking()
            .Where( c => c.IsPublished )
            .OrderBy( c => c.TitleEn );

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( c => new CourseListItemDto(
                c.Id ,
                isEl ? ( c.TitleEl ?? c.TitleEn ) : c.TitleEn ,
                isEl ? ( c.DescriptionEl ?? c.DescriptionEn ) : c.DescriptionEn ,
                c.Enrollments.Any( e => e.UserId == userId ) ,
                c.Modules.SelectMany( m => m.Lessons ).Count() ,
                c.RequiredTier
            ) )
            .ToListAsync();

        return new PagedResult<CourseListItemDto>( items , totalCount , page , pageSize );
    }

    public async Task<CourseDetailDto?> GetCourseDetailAsync( string userId , Guid courseId , string lang )
    {
        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var course = await db.Courses
            .AsNoTracking()
            .Include( c => c.Modules.OrderBy( m => m.SortOrder ) )
                .ThenInclude( m => m.Lessons.OrderBy( l => l.SortOrder ) )
            .Include( c => c.Enrollments.Where( e => e.UserId == userId ) )
            .FirstOrDefaultAsync( c => c.Id == courseId && c.IsPublished );

        if ( course is null )
            return null;

        var enrollment = course.Enrollments.FirstOrDefault();
        var allLessonIds = course.Modules.SelectMany( m => m.Lessons ).Select( l => l.Id ).ToList();
        var completedLessonIds = await db.LessonCompletions
            .AsNoTracking()
            .Where( lc => lc.UserId == userId && allLessonIds.Contains( lc.LessonId ) )
            .Select( lc => lc.LessonId )
            .ToHashSetAsync();

        var totalLessons = allLessonIds.Count;
        var completedLessons = completedLessonIds.Count;
        var progressPercent = totalLessons > 0 ? Math.Round( ( double ) completedLessons / totalLessons * 100 , 1 ) : 0;

        return new CourseDetailDto(
            course.Id ,
            isEl ? ( course.TitleEl ?? course.TitleEn ) : course.TitleEn ,
            isEl ? ( course.DescriptionEl ?? course.DescriptionEn ) : course.DescriptionEn ,
            enrollment is not null ,
            enrollment?.Status == EnrollmentStatus.Completed ,
            completedLessons ,
            totalLessons ,
            progressPercent ,
            course.Modules.Select( m => new ModuleDto(
                m.Id ,
                isEl ? ( m.TitleEl ?? m.TitleEn ) : m.TitleEn ,
                isEl ? ( m.DescriptionEl ?? m.DescriptionEn ) : m.DescriptionEn ,
                m.SortOrder ,
                m.Lessons.Select( l => new LessonSummaryDto(
                    l.Id ,
                    isEl ? ( l.TitleEl ?? l.TitleEn ) : l.TitleEn ,
                    (int) GetContentType( l ) ,
                    l.DurationMinutes ,
                    l.SortOrder ,
                    completedLessonIds.Contains( l.Id )
                ) ).ToList()
            ) ).ToList() ,
            course.RequiredTier
        );
    }

    public async Task<ServiceResult<EnrollmentResultDto>> EnrollAsync( string userId , Guid courseId )
    {
        var course = await db.Courses.FirstOrDefaultAsync( c => c.Id == courseId && c.IsPublished );
        if ( course is null )
            return ServiceResult<EnrollmentResultDto>.NotFound( new EnrollmentResultDto( false , "Course not found" , null ) );

        var userStatus = await subscriptionService.GetUserStatusAsync( userId );
        if ( userStatus.Tier < course.RequiredTier )
        {
            return ServiceResult<EnrollmentResultDto>.Forbidden( new EnrollmentResultDto(
                false ,
                $"This course requires a {course.RequiredTier} subscription or higher. Please upgrade your plan." ,
                null ) );
        }

        var maxCourses = userStatus.Features?.MaxCourses ?? 1;
        if ( maxCourses != int.MaxValue )
        {
            var enrollmentCount = await db.Enrollments.CountAsync( e => e.UserId == userId );
            if ( enrollmentCount >= maxCourses )
            {
                return ServiceResult<EnrollmentResultDto>.Forbidden( new EnrollmentResultDto(
                    false ,
                    $"Your {userStatus.PlanName} plan allows up to {maxCourses} course(s). Please upgrade to enroll in more courses." ,
                    null ) );
            }
        }

        var existing = await db.Enrollments
            .FirstOrDefaultAsync( e => e.UserId == userId && e.CourseId == courseId );

        if ( existing is not null )
            return ServiceResult<EnrollmentResultDto>.Ok( new EnrollmentResultDto( true , "Already enrolled" , existing.Id ) );

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid() ,
            UserId = userId ,
            CourseId = courseId ,
            EnrolledAt = DateTime.UtcNow ,
            Status = EnrollmentStatus.Active
        };

        db.Enrollments.Add( enrollment );
        await db.SaveChangesAsync();

        logger.LogInformation( "User {UserId} enrolled in course {CourseId}" , userId , courseId );

        return ServiceResult<EnrollmentResultDto>.Ok( new EnrollmentResultDto( true , "Enrolled successfully" , enrollment.Id ) );
    }

    public async Task<ServiceResult<LessonDetailDto>> GetLessonDetailAsync( string userId , Guid lessonId , string lang )
    {
        var isEl = string.Equals( lang , "el" , StringComparison.OrdinalIgnoreCase );

        var lesson = await db.Lessons
            .AsNoTracking()
            .Include( l => l.Module )
                .ThenInclude( m => m.Course )
            .FirstOrDefaultAsync( l => l.Id == lessonId );

        if ( lesson is null )
            return ServiceResult<LessonDetailDto>.NotFound( error: "Lesson not found" );

        var course = lesson.Module.Course;
        if ( !course.IsPublished )
            return ServiceResult<LessonDetailDto>.NotFound( error: "Course not found" );

        var isEnrolled = await db.Enrollments
            .AnyAsync( e => e.UserId == userId && e.CourseId == course.Id );

        if ( !isEnrolled )
            return ServiceResult<LessonDetailDto>.BadRequest( error: "You must be enrolled in this course to view lessons" );

        var isCompleted = await db.LessonCompletions
            .AnyAsync( lc => lc.UserId == userId && lc.LessonId == lessonId );

        var allLessons = await db.Lessons
            .AsNoTracking()
            .Where( l => l.Module.CourseId == course.Id )
            .OrderBy( l => l.Module.SortOrder )
            .ThenBy( l => l.SortOrder )
            .Select( l => l.Id )
            .ToListAsync();

        var currentIndex = allLessons.IndexOf( lessonId );
        var previousLessonId = currentIndex > 0 ? allLessons [ currentIndex - 1 ] : ( Guid? ) null;
        var nextLessonId = currentIndex < allLessons.Count - 1 ? allLessons [ currentIndex + 1 ] : ( Guid? ) null;

        var contentType = GetContentType( lesson );
        var displayContent = contentType == ContentType.Video
            ? lesson.VideoPath
            : ( isEl ? ( lesson.ContentEl ?? lesson.ContentEn ) : lesson.ContentEn );

        var dto = new LessonDetailDto(
            lesson.Id ,
            isEl ? ( lesson.TitleEl ?? lesson.TitleEn ) : lesson.TitleEn ,
            (int) contentType ,
            displayContent ,
            lesson.PdfPath ,
            lesson.DurationMinutes ,
            isCompleted ,
            lesson.ModuleId ,
            isEl ? ( lesson.Module.TitleEl ?? lesson.Module.TitleEn ) : lesson.Module.TitleEn ,
            course.Id ,
            isEl ? ( course.TitleEl ?? course.TitleEn ) : course.TitleEn ,
            previousLessonId ,
            nextLessonId
        );

        return ServiceResult<LessonDetailDto>.Ok( dto );
    }

    public async Task<ServiceResult<LessonCompletionResultDto>> CompleteLessonAsync( string userId , Guid lessonId )
    {
        var lesson = await db.Lessons
            .Include( l => l.Module )
            .FirstOrDefaultAsync( l => l.Id == lessonId );

        if ( lesson is null )
            return ServiceResult<LessonCompletionResultDto>.NotFound(
                new LessonCompletionResultDto( false , "Lesson not found" , 0 , 0 , 0 , false ) );

        var courseId = lesson.Module.CourseId;

        var enrollment = await db.Enrollments
            .FirstOrDefaultAsync( e => e.UserId == userId && e.CourseId == courseId );

        if ( enrollment is null )
            return ServiceResult<LessonCompletionResultDto>.BadRequest(
                new LessonCompletionResultDto( false , "Not enrolled in this course" , 0 , 0 , 0 , false ) );

        var existingCompletion = await db.LessonCompletions
            .FirstOrDefaultAsync( lc => lc.UserId == userId && lc.LessonId == lessonId );

        if ( existingCompletion is null )
        {
            var completion = new LessonCompletion
            {
                Id = Guid.NewGuid() ,
                UserId = userId ,
                LessonId = lessonId ,
                CompletedAt = DateTime.UtcNow
            };
            db.LessonCompletions.Add( completion );
        }

        var allLessonIds = await db.Lessons
            .Where( l => l.Module.CourseId == courseId )
            .Select( l => l.Id )
            .ToListAsync();

        var completedCount = await db.LessonCompletions
            .CountAsync( lc => lc.UserId == userId && allLessonIds.Contains( lc.LessonId ) );

        if ( existingCompletion is null )
            completedCount++;

        var totalLessons = allLessonIds.Count;
        var progressPercent = totalLessons > 0 ? Math.Round( ( double ) completedCount / totalLessons * 100 , 1 ) : 0;
        var courseCompleted = completedCount >= totalLessons;

        if ( courseCompleted && enrollment.Status != EnrollmentStatus.Completed )
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.CompletedAt = DateTime.UtcNow;
            logger.LogInformation( "User {UserId} completed course {CourseId}" , userId , courseId );
        }

        await db.SaveChangesAsync();

        if ( courseCompleted )
        {
            try
            {
                await certificateService.GetOrGenerateAsync( userId , courseId );
            }
            catch ( Exception ex )
            {
                logger.LogError( ex ,
                    "Certificate auto-generation failed for user {UserId} on course {CourseId}." ,
                    userId , courseId );
            }
        }

        return ServiceResult<LessonCompletionResultDto>.Ok( new LessonCompletionResultDto(
            true ,
            existingCompletion is null ? "Lesson completed" : "Already completed" ,
            completedCount ,
            totalLessons ,
            progressPercent ,
            courseCompleted
        ) );
    }

    private static ContentType GetContentType( Lesson lesson )
    {
        if ( !string.IsNullOrEmpty( lesson.VideoPath ) )
            return ContentType.Video;
        if ( !string.IsNullOrEmpty( lesson.PdfPath ) )
            return ContentType.Pdf;
        return ContentType.Text;
    }
}
