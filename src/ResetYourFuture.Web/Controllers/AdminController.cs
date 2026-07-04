using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using System.Security.Claims;


namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin-only endpoints for user and role management.
/// Students are hard-blocked from these routes.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Users & Roles")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminController : ControllerBase
{
    // Identity UserManager used to manage and query application users.
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AdminController> _logger;
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        ILogger<AdminController> logger,
        IApplicationDbContext context,
        IEmailService emailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _logger = logger;
        _context = context;
        _emailService = emailService;
    }

    /// <summary>
    /// List users with server-side pagination and optional search.
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "email",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.ApplySearch(search.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .ApplySort(sortBy, sortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Single query: fetch all (userId → roleName) pairs for the current page
        var userIds = users.Select(u => u.Id).ToList();
        var userRolePairs = await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles,
                   ur => ur.RoleId,
                   r => r.Id,
                   (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var userRoleMap = userRolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        var result = users.Select(user =>
        {
            var roles = userRoleMap.TryGetValue(user.Id, out var r) ? r : [];
            return new AdminUserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.DisplayName,
                user.EmailConfirmed,
                user.IsEnabled,
                user.Status.ToString(),
                [.. roles],
                user.CreatedAt
            );
        }).ToList();

        return Ok(new PagedResult<AdminUserDto>(result, totalCount, page, pageSize, sortBy, sortDir));
    }

    /// <summary>
    /// Get single user by ID.
    /// </summary>
    [HttpGet("users/{userId}")]
    [ProducesResponseType<AdminUserDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUserDetailDto>> GetUser(string userId)
    {
        // Find the user by id; return 404 if not found.
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        // Fetch user roles and return a detailed user object.
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new AdminUserDetailDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Age,
            user.Status.ToString(),
            user.EmailConfirmed,
            user.IsEnabled,
            user.GdprConsentGiven,
            user.GdprConsentDate,
            user.ParentalConsentGiven,
            user.CreatedAt,
            [.. roles]));
    }

    /// <summary>
    /// Assign a role to a user.
    /// </summary>
    [HttpPost("users/{userId}/roles/{roleName}")]
    public async Task<ActionResult> AssignRole(string userId, string roleName)
    {
        // Ensure the user exists before operating on roles.
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        // Validate that the role exists.
        if (!await _roleManager.RoleExistsAsync(roleName))
            return BadRequest($"Role '{roleName}' does not exist.");

        // Prevent assigning the same role twice.
        if (await _userManager.IsInRoleAsync(user, roleName))
            return BadRequest($"User already has role '{roleName}'.");

        // Add the role and handle potential errors.
        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Log the assignment and return success.
        _logger.LogInformation("Admin assigned role {Role} to user {UserId}", roleName, userId);
        return Ok($"Role '{roleName}' assigned.");
    }

    /// <summary>
    /// Remove a role from a user.
    /// </summary>
    [HttpDelete("users/{userId}/roles/{roleName}")]
    public async Task<ActionResult> RemoveRole(string userId, string roleName)
    {
        // Ensure the user exists.
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        // Ensure the user actually has the role before attempting removal.
        if (!await _userManager.IsInRoleAsync(user, roleName))
            return BadRequest($"User does not have role '{roleName}'.");

        // Remove the role and handle errors.
        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Log removal and return success.
        _logger.LogInformation("Admin removed role {Role} from user {UserId}", roleName, userId);
        return Ok($"Role '{roleName}' removed.");
    }

    /// <summary>
    /// List all roles.
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles()
    {
        // Project roles to their names and return.
        var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        return Ok(roles);
    }

    /// <summary>
    /// Create a new role.
    /// </summary>
    [HttpPost("roles/{roleName}")]
    public async Task<ActionResult> CreateRole(string roleName)
    {
        // Prevent creating duplicate roles.
        if (await _roleManager.RoleExistsAsync(roleName))
            return BadRequest($"Role '{roleName}' already exists.");

        // Create the role and check result for failures.
        var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Log creation and return success.
        _logger.LogInformation("Admin created role {Role}", roleName);
        return Ok($"Role '{roleName}' created.");
    }

    /// <summary>
    /// Toggle IsEnabled for a user.
    /// </summary>
    [HttpPost("users/{userId}/toggle-enable")]
    public async Task<ActionResult> ToggleEnable(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        // Prevent disabling Admin-role users.
        if (await _userManager.IsInRoleAsync(user, "Admin"))
            return BadRequest("Admin accounts cannot be disabled.");

        user.IsEnabled = !user.IsEnabled;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        _logger.LogInformation("Admin toggled IsEnabled to {IsEnabled} for user {UserId}", user.IsEnabled, userId);
        return Ok(new UserEnabledStateDto(user.IsEnabled));
    }

    /// <summary>
    /// Delete user (GDPR data deletion). Soft-delete recommended for production.
    /// </summary>
    [HttpDelete("users/{userId}")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        // Find the user and return 404 if missing.
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        // Prevent deletion of Admin-role users.
        if (await _userManager.IsInRoleAsync(user, "Admin"))
            return BadRequest("Admin accounts cannot be deleted.");

        // Hard delete for now; prefer soft-delete in production.
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Log deletion and acknowledge success.
        _logger.LogInformation("Admin deleted user {UserId}", userId);
        return Ok("User deleted.");
    }

    /// <summary>
    /// Search users by email or name.
    /// </summary>
    [HttpGet("users/search")]
    [ProducesResponseType<IEnumerable<AdminUserSearchResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AdminUserSearchResultDto>>> SearchUsers([FromQuery] string query)
    {
        // Validate query input.
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query is required");
        }

        // Query the Identity store for matching users (limit to 50).
        var users = await _userManager.Users
            .AsNoTracking()
            .ApplySearch(query.Trim())
            .Take(50)
            .ToListAsync();

        // Single JOIN query for all roles — avoids N+1 round-trips.
        var userIds = users.Select(u => u.Id).ToList();
        var userRolePairs = await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles,
                   ur => ur.RoleId,
                   r => r.Id,
                   (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync();

        var userRoleMap = userRolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        var result = users.Select(user => new AdminUserSearchResultDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.EmailConfirmed,
            userRoleMap.TryGetValue(user.Id, out var r) ? r : new List<string>()
        ));

        return Ok(result);
    }

    /// <summary>
    /// Force-sends a password reset email to a user (admin action).
    /// The token is delivered out-of-band via email — never returned to the caller.
    /// </summary>
    [HttpPost("users/{userId}/force-password-reset")]
    public async Task<IActionResult> ForcePasswordReset(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found");

        if (string.IsNullOrEmpty(user.Email))
            return BadRequest("User has no email address.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = Url.Action(
            "ResetPassword", "Auth",
            new { email = user.Email, token },
            Request.Scheme) ?? $"{Request.Scheme}://{Request.Host}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

        await _emailService.SendPasswordResetAsync(user.Email, resetUrl);
        _logger.LogInformation("Admin triggered forced password reset for user {UserId}.", userId);

        return NoContent();
    }

    /// <summary>
    /// Disable user account (lockout).
    /// </summary>
    [HttpPost("users/{userId}/disable")]
    public async Task<IActionResult> DisableUser(string userId)
    {
        // Find the user and return 404 if not present.
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found");

        // Set lockout end date to effectively disable login.
        var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        // Log and return NoContent to indicate success.
        _logger.LogInformation("Admin disabled user {UserId}", userId);
        return NoContent();
    }

    /// <summary>
    /// Enable user account (remove lockout).
    /// </summary>
    [HttpPost("users/{userId}/enable")]
    public async Task<IActionResult> EnableUser(string userId)
    {
        // Find the user and return 404 if not present.
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found");

        // Clear lockout to allow the user to login again.
        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        // Log and return NoContent.
        _logger.LogInformation("Admin enabled user {UserId}", userId);
        return NoContent();
    }

    /// <summary>
    /// Generates a short-lived JWT so an admin can view the platform as a specific student.
    /// Returns no refresh token — the session is temporary and cannot be silently extended.
    /// </summary>
    [HttpPost("users/{userId}/impersonate")]
    public async Task<ActionResult<AuthResponseDto>> ImpersonateUser(string userId)
    {
        var target = await _userManager.FindByIdAsync(userId);
        if (target is null)
            return NotFound("User not found.");

        var targetRoles = await _userManager.GetRolesAsync(target);
        if (!targetRoles.Contains("Student"))
            return BadRequest("Only Student accounts can be impersonated.");

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var (token, expiration) = await _tokenService.GenerateImpersonationTokenAsync(target, adminId);

        _logger.LogInformation("Admin {AdminId} started impersonating user {UserId}", adminId, userId);

        return Ok(new AuthResponseDto
        {
            Success = true,
            Token = token,
            Expiration = expiration
        });
    }

    /// <summary>
    /// Directly set a new password for any user (admin override, no token email required).
    /// </summary>
    [HttpPost("users/{userId}/set-password")]
    public async Task<IActionResult> SetPassword(string userId, [FromBody] AdminSetPasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            return BadRequest("Admin account passwords cannot be changed from the user table.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        _logger.LogInformation("Admin set new password for user {UserId}", userId);
        return Ok();
    }
}
