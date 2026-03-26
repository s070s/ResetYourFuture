using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Enums;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin analytics and statistics endpoints.
/// </summary>
[ApiController]
[Route( "api/admin/analytics" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminAnalyticsController : ControllerBase
{
    // EF Core DB context used to query application data (courses, enrollments, etc.)
    private readonly ApplicationDbContext _db;
    // Identity user manager used to query and manage application users and their roles
    private readonly UserManager<ApplicationUser> _userManager;

    // Constructor receives dependencies via dependency injection
    public AdminAnalyticsController( ApplicationDbContext db , UserManager<ApplicationUser> userManager )
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// Get analytics summary for admin dashboard.
    /// </summary>
    [HttpGet( "summary" )]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetSummary()
    {
        var totalUsers = await _userManager.Users.CountAsync();

        var activeUsers = await _db.Enrollments
            .Select( e => e.UserId )
            .Distinct()
            .CountAsync();

        var totalEnrollments = await _db.Enrollments.CountAsync();

        var completedCourses = await _db.Enrollments
            .CountAsync( e => e.Status == EnrollmentStatus.Completed );

        var enrollmentData = await _db.Enrollments
            .Select( e => new { e.CourseId, CourseTitle = e.Course.TitleEn, e.Status } )
            .ToListAsync();

        var courseStats = enrollmentData
            .GroupBy( e => new { e.CourseId, e.CourseTitle } )
            .Select( g => new CourseStatDto(
                g.Key.CourseTitle,
                g.Count(),
                g.Count( e => e.Status == EnrollmentStatus.Completed )
            ) )
            .ToList();

        var submissionData = await _db.AssessmentSubmissions
            .Select( s => new { s.AssessmentDefinitionId, AssessmentTitle = s.AssessmentDefinition.TitleEn } )
            .ToListAsync();

        var assessmentStats = submissionData
            .GroupBy( s => new { s.AssessmentDefinitionId, s.AssessmentTitle } )
            .Select( g => new AssessmentStatDto( g.Key.AssessmentTitle, g.Count() ) )
            .ToList();

        var dto = new AnalyticsSummaryDto(
            totalUsers,
            activeUsers,
            totalEnrollments,
            completedCourses,
            courseStats,
            assessmentStats
        );

        return Ok( dto );
    }
}
