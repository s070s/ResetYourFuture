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
    // EF Core DB context used to query application data (courses, enrollments, etc.)
    private readonly ApplicationDbContext _db;
    // Identity user manager used to query and manage application users and their roles
    private readonly UserManager<ApplicationUser> _userManager;

    // Constructor receives dependencies via dependency injection
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
        // Count total users using the Identity user store (efficient SQL COUNT)
        var totalUsers = await _userManager.Users.CountAsync();
        
        // Load all users into memory so we can check each user's roles
        var allUsers = await _userManager.Users.ToListAsync();
        // Lists to separate admin and student users for simple counting
        var adminUsers = new List<ApplicationUser>();
        var studentUsers = new List<ApplicationUser>();
        
        // Iterate users and classify by role; GetRolesAsync queries the role table for each user
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

        // Compute totals from the classified lists
        var totalAdmins = adminUsers.Count;
        var totalStudents = studentUsers.Count;
        // Use EF Core counts to get totals for courses and related entities
        var totalCourses = await _db.Courses.CountAsync();
        var publishedCourses = await _db.Courses.CountAsync(c => c.IsPublished);
        var activeEnrollments = await _db.Enrollments.CountAsync();
        var totalAssessmentSubmissions = await _db.AssessmentSubmissions.CountAsync();

        // Create the DTO that will be serialized to JSON and returned to the client
        var dto = new AnalyticsSummaryDto(
            totalUsers,
            totalStudents,
            totalAdmins,
            totalCourses,
            publishedCourses,
            activeEnrollments,
            totalAssessmentSubmissions
        );

        // Return 200 OK with the DTO payload
        return Ok(dto);
    }
}
