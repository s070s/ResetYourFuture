using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// API endpoints for course discovery, enrollment, and consumption.
/// All endpoints require authentication.
/// </summary>
[ApiController]
[Route( "api/[controller]" )]
[Authorize]
public class CoursesController( ICourseService courseService ) : ControllerBase
{
    private string UserId => User.FindFirstValue( ClaimTypes.NameIdentifier )
        ?? throw new UnauthorizedAccessException( "User ID not found in claims" );

    /// <summary>
    /// Get a paged list of all published courses with enrollment status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseListItemDto>>> GetCourses(
        [FromQuery] int page = 1 ,
        [FromQuery] int pageSize = 10 ,
        [FromQuery] string lang = "en" )
    {
        if ( page < 1 )
            page = 1;
        if ( pageSize < 1 || pageSize > 100 )
            pageSize = 10;

        var result = await courseService.GetPublishedCoursesAsync( UserId , page , pageSize , lang );
        return Ok( result );
    }

    /// <summary>
    /// Get full course detail including modules, lessons, and progress.
    /// </summary>
    [HttpGet( "{courseId:guid}" )]
    public async Task<ActionResult<CourseDetailDto>> GetCourse( Guid courseId , [FromQuery] string lang = "en" )
    {
        var dto = await courseService.GetCourseDetailAsync( UserId , courseId , lang );
        return dto is not null ? Ok( dto ) : NotFound( "Course not found" );
    }

    /// <summary>
    /// Enroll the current user in a course.
    /// Admins cannot enroll - this is for students only.
    /// </summary>
    [HttpPost( "{courseId:guid}/enroll" )]
    public async Task<ActionResult<EnrollmentResultDto>> Enroll( Guid courseId )
    {
        if ( User.IsInRole( "Admin" ) )
            return StatusCode( 403 , new EnrollmentResultDto( false , "Administrators cannot enroll in courses" , null ) );

        var result = await courseService.EnrollAsync( UserId , courseId );
        return StatusCode( result.StatusCode , result.Value );
    }

    /// <summary>
    /// Get full lesson detail for the lesson viewer.
    /// </summary>
    [HttpGet( "lessons/{lessonId:guid}" )]
    public async Task<ActionResult<LessonDetailDto>> GetLesson( Guid lessonId , [FromQuery] string lang = "en" )
    {
        var result = await courseService.GetLessonDetailAsync( UserId , lessonId , lang );
        if ( !result.IsSuccess )
            return StatusCode( result.StatusCode , result.ErrorMessage );
        return Ok( result.Value );
    }

    /// <summary>
    /// Mark a lesson as completed.
    /// </summary>
    [HttpPost( "lessons/{lessonId:guid}/complete" )]
    public async Task<ActionResult<LessonCompletionResultDto>> CompleteLesson( Guid lessonId )
    {
        var result = await courseService.CompleteLessonAsync( UserId , lessonId );
        return StatusCode( result.StatusCode , result.Value );
    }
}
