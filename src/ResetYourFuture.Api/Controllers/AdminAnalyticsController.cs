using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
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

        // Single JOIN + COUNT query — eliminates the N+1 GetRolesAsync loop
        var totalAdmins = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where r.NormalizedName == "ADMIN"
            select ur.UserId
        ).CountAsync();

        var totalStudents = totalUsers - totalAdmins;
        var totalCourses = await _db.Courses.CountAsync();
        var publishedCourses = await _db.Courses.CountAsync( c => c.IsPublished );
        var activeEnrollments = await _db.Enrollments.CountAsync();
        var totalAssessmentSubmissions = await _db.AssessmentSubmissions.CountAsync();

        var dto = new AnalyticsSummaryDto(
            totalUsers,
            totalStudents,
            totalAdmins,
            totalCourses,
            publishedCourses,
            activeEnrollments,
            totalAssessmentSubmissions
        );

        return Ok( dto );
    }
}
