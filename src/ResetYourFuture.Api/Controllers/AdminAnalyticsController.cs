using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Shared.Models.Admin;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Admin analytics and statistics endpoints.
/// </summary>
[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = "AdminOnly")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminAnalyticsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// Get analytics summary for admin dashboard.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetSummary()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        
        var allUsers = await _userManager.Users.ToListAsync();
        var adminUsers = new List<ApplicationUser>();
        var studentUsers = new List<ApplicationUser>();
        
        foreach (var user in allUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
                adminUsers.Add(user);
            }
            else
            {
                studentUsers.Add(user);
            }
        }

        var totalAdmins = adminUsers.Count;
        var totalStudents = studentUsers.Count;
        var totalCourses = await _db.Courses.CountAsync();
        var publishedCourses = await _db.Courses.CountAsync(c => c.IsPublished);
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

        return Ok(dto);
    }
}
