using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Extensions;
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
public class AdminController(IAdminUserService adminUserService) : ControllerBase
{
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

        var result = await adminUserService.GetUsersAsync(page, pageSize, search, sortBy, sortDir, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get single user by ID.
    /// </summary>
    [HttpGet("users/{userId}")]
    [ProducesResponseType<AdminUserDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUserDetailDto>> GetUser(string userId, CancellationToken cancellationToken = default)
    {
        var dto = await adminUserService.GetUserAsync(userId, cancellationToken);
        return dto is not null ? Ok(dto) : NotFound();
    }

    /// <summary>
    /// Assign a role to a user.
    /// </summary>
    [HttpPost("users/{userId}/roles/{roleName}")]
    public async Task<ActionResult<string>> AssignRole(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.AssignRoleAsync(userId, roleName, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Remove a role from a user.
    /// </summary>
    [HttpDelete("users/{userId}/roles/{roleName}")]
    public async Task<ActionResult<string>> RemoveRole(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.RemoveRoleAsync(userId, roleName, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// List all roles.
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles(CancellationToken cancellationToken = default)
    {
        var roles = await adminUserService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Create a new role.
    /// </summary>
    [HttpPost("roles/{roleName}")]
    public async Task<ActionResult<string>> CreateRole(string roleName, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.CreateRoleAsync(roleName, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Toggle IsEnabled for a user.
    /// </summary>
    [HttpPost("users/{userId}/toggle-enable")]
    public async Task<ActionResult<UserEnabledStateDto>> ToggleEnable(string userId, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.ToggleEnableAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete user (GDPR data deletion). Soft-delete recommended for production.
    /// </summary>
    [HttpDelete("users/{userId}")]
    public async Task<ActionResult<string>> DeleteUser(string userId, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.DeleteUserAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Search users by email or name.
    /// </summary>
    [HttpGet("users/search")]
    [ProducesResponseType<IEnumerable<AdminUserSearchResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AdminUserSearchResultDto>>> SearchUsers([FromQuery] string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query is required");

        var result = await adminUserService.SearchUsersAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Force-sends a password reset email to a user (admin action).
    /// The token is delivered out-of-band via email — never returned to the caller.
    /// </summary>
    [HttpPost("users/{userId}/force-password-reset")]
    public async Task<ActionResult<bool>> ForcePasswordReset(string userId, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.ForcePasswordResetAsync(userId, (email, token) =>
            Url.Action(
                "ResetPassword", "Auth",
                new { email, token },
                Request.Scheme) ?? $"{Request.Scheme}://{Request.Host}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}",
            cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Disable user account (lockout).
    /// </summary>
    [HttpPost("users/{userId}/disable")]
    public async Task<ActionResult<bool>> DisableUser(string userId, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.DisableUserAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Enable user account (remove lockout).
    /// </summary>
    [HttpPost("users/{userId}/enable")]
    public async Task<ActionResult<bool>> EnableUser(string userId, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.EnableUserAsync(userId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Generates a short-lived JWT so an admin can view the platform as a specific student.
    /// Returns no refresh token — the session is temporary and cannot be silently extended.
    /// </summary>
    [HttpPost("users/{userId}/impersonate")]
    public async Task<ActionResult<AuthResponseDto>> ImpersonateUser(string userId, CancellationToken cancellationToken = default)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await adminUserService.ImpersonateUserAsync(userId, adminId, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Directly set a new password for any user (admin override, no token email required).
    /// </summary>
    [HttpPost("users/{userId}/set-password")]
    public async Task<ActionResult<bool>> SetPassword(string userId, [FromBody] AdminSetPasswordDto dto, CancellationToken cancellationToken = default)
    {
        var result = await adminUserService.SetPasswordAsync(userId, dto.NewPassword, cancellationToken);
        return result.IsSuccess ? Ok() : result.ToActionResult();
    }
}
