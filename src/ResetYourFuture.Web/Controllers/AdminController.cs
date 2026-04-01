using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Extensions;
using ResetYourFuture.Web.Identity;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;


namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin-only endpoints for user and role management.
/// Students are hard-blocked from these routes.
/// </summary>
[ApiController]
[Route( "api/[controller]" )]
[Authorize( Policy = "AdminOnly" )]
public class AdminController : ControllerBase
{
    // Identity UserManager used to manage and query application users.
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AdminController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IBlogArticleService _blogService;
    private readonly IFileStorage _fileStorage;

    public AdminController(
        UserManager<ApplicationUser> userManager ,
        RoleManager<IdentityRole> roleManager ,
        ITokenService tokenService ,
        ILogger<AdminController> logger ,
        ApplicationDbContext context ,
        IBlogArticleService blogService ,
        IFileStorage fileStorage )
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _logger = logger;
        _context = context;
        _blogService = blogService;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// List users with server-side pagination and optional search.
    /// </summary>
    [HttpGet( "users" )]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "email",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default )
    {
        page = Math.Max( 1, page );
        pageSize = Math.Clamp( pageSize, 1, 100 );

        var query = _userManager.Users.AsNoTracking();

        if ( !string.IsNullOrWhiteSpace( search ) )
        {
            query = query.ApplySearch( search.Trim() );
        }

        var totalCount = await query.CountAsync( cancellationToken );

        var users = await query
            .ApplySort( sortBy, sortDir )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .ToListAsync( cancellationToken );

        // Single query: fetch all (userId → roleName) pairs for the current page
        var userIds = users.Select( u => u.Id ).ToList();
        var userRolePairs = await _context.UserRoles
            .Where( ur => userIds.Contains( ur.UserId ) )
            .Join( _context.Roles,
                   ur => ur.RoleId,
                   r => r.Id,
                   ( ur, r ) => new { ur.UserId, r.Name } )
            .ToListAsync( cancellationToken );

        var userRoleMap = userRolePairs
            .GroupBy( x => x.UserId )
            .ToDictionary( g => g.Key, g => g.Select( x => x.Name! ).ToList() );

        var result = users.Select( user =>
        {
            var roles = userRoleMap.TryGetValue( user.Id, out var r ) ? r : [];
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
        } ).ToList();

        return Ok( new PagedResult<AdminUserDto>( result, totalCount, page, pageSize, sortBy, sortDir ) );
    }

    /// <summary>
    /// Get single user by ID.
    /// </summary>
    [HttpGet( "users/{userId}" )]
    public async Task<ActionResult<object>> GetUser( string userId )
    {
        // Find the user by id; return 404 if not found.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound();

        // Fetch user roles and return a detailed user object.
        var roles = await _userManager.GetRolesAsync( user );
        return Ok( new
        {
            user.Id ,
            user.Email ,
            user.FirstName ,
            user.LastName ,
            user.Age ,
            Status = user.Status.ToString() ,
            user.EmailConfirmed ,
            user.IsEnabled ,
            user.GdprConsentGiven ,
            user.GdprConsentDate ,
            user.ParentalConsentGiven ,
            user.CreatedAt ,
            Roles = roles
        } );
    }

    /// <summary>
    /// Assign a role to a user.
    /// </summary>
    [HttpPost( "users/{userId}/roles/{roleName}" )]
    public async Task<ActionResult> AssignRole( string userId , string roleName )
    {
        // Ensure the user exists before operating on roles.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found." );

        // Validate that the role exists.
        if ( !await _roleManager.RoleExistsAsync( roleName ) )
            return BadRequest( $"Role '{roleName}' does not exist." );

        // Prevent assigning the same role twice.
        if ( await _userManager.IsInRoleAsync( user , roleName ) )
            return BadRequest( $"User already has role '{roleName}'." );

        // Add the role and handle potential errors.
        var result = await _userManager.AddToRoleAsync( user , roleName );
        if ( !result.Succeeded )
            return BadRequest( result.Errors.Select( e => e.Description ) );

        // Log the assignment and return success.
        _logger.LogInformation( "Admin assigned role {Role} to user {UserId}" , roleName , userId );
        return Ok( $"Role '{roleName}' assigned." );
    }

    /// <summary>
    /// Remove a role from a user.
    /// </summary>
    [HttpDelete( "users/{userId}/roles/{roleName}" )]
    public async Task<ActionResult> RemoveRole( string userId , string roleName )
    {
        // Ensure the user exists.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found." );

        // Ensure the user actually has the role before attempting removal.
        if ( !await _userManager.IsInRoleAsync( user , roleName ) )
            return BadRequest( $"User does not have role '{roleName}'." );

        // Remove the role and handle errors.
        var result = await _userManager.RemoveFromRoleAsync( user , roleName );
        if ( !result.Succeeded )
            return BadRequest( result.Errors.Select( e => e.Description ) );

        // Log removal and return success.
        _logger.LogInformation( "Admin removed role {Role} from user {UserId}" , roleName , userId );
        return Ok( $"Role '{roleName}' removed." );
    }

    /// <summary>
    /// List all roles.
    /// </summary>
    [HttpGet( "roles" )]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles()
    {
        // Project roles to their names and return.
        var roles = await _roleManager.Roles.Select( r => r.Name ).ToListAsync();
        return Ok( roles );
    }

    /// <summary>
    /// Create a new role.
    /// </summary>
    [HttpPost( "roles/{roleName}" )]
    public async Task<ActionResult> CreateRole( string roleName )
    {
        // Prevent creating duplicate roles.
        if ( await _roleManager.RoleExistsAsync( roleName ) )
            return BadRequest( $"Role '{roleName}' already exists." );

        // Create the role and check result for failures.
        var result = await _roleManager.CreateAsync( new IdentityRole( roleName ) );
        if ( !result.Succeeded )
            return BadRequest( result.Errors.Select( e => e.Description ) );

        // Log creation and return success.
        _logger.LogInformation( "Admin created role {Role}" , roleName );
        return Ok( $"Role '{roleName}' created." );
    }

    /// <summary>
    /// Toggle IsEnabled for a user.
    /// </summary>
    [HttpPost( "users/{userId}/toggle-enable" )]
    public async Task<ActionResult> ToggleEnable( string userId )
    {
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found." );

        // Prevent disabling Admin-role users.
        if ( await _userManager.IsInRoleAsync( user, "Admin" ) )
            return BadRequest( "Admin accounts cannot be disabled." );

        user.IsEnabled = !user.IsEnabled;
        var result = await _userManager.UpdateAsync( user );
        if ( !result.Succeeded )
            return BadRequest( result.Errors.Select( e => e.Description ) );

        _logger.LogInformation( "Admin toggled IsEnabled to {IsEnabled} for user {UserId}" , user.IsEnabled , userId );
        return Ok( new
        {
            user.IsEnabled
        } );
    }

    /// <summary>
    /// Delete user (GDPR data deletion). Soft-delete recommended for production.
    /// </summary>
    [HttpDelete( "users/{userId}" )]
    public async Task<ActionResult> DeleteUser( string userId )
    {
        // Find the user and return 404 if missing.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found." );

        // Prevent deletion of Admin-role users.
        if ( await _userManager.IsInRoleAsync( user, "Admin" ) )
            return BadRequest( "Admin accounts cannot be deleted." );

        // Hard delete for now; prefer soft-delete in production.
        var result = await _userManager.DeleteAsync( user );
        if ( !result.Succeeded )
            return BadRequest( result.Errors.Select( e => e.Description ) );

        // Log deletion and acknowledge success.
        _logger.LogInformation( "Admin deleted user {UserId}" , userId );
        return Ok( "User deleted." );
    }

    /// <summary>
    /// Search users by email or name.
    /// </summary>
    [HttpGet( "users/search" )]
    public async Task<ActionResult<IEnumerable<object>>> SearchUsers( [FromQuery] string query )
    {
        // Validate query input.
        if ( string.IsNullOrWhiteSpace( query ) )
        {
            return BadRequest( "Search query is required" );
        }

        // Query the Identity store for matching users (limit to 50).
        var users = await _userManager.Users
            .AsNoTracking()
            .ApplySearch( query.Trim() )
            .Take( 50 )
            .ToListAsync();

        // Build response objects including roles for each user.
        var result = new List<object>();
        foreach ( var user in users )
        {
            var roles = await _userManager.GetRolesAsync( user );
            result.Add( new
            {
                user.Id ,
                user.Email ,
                user.FirstName ,
                user.LastName ,
                user.DisplayName ,
                user.EmailConfirmed ,
                Roles = roles
            } );
        }

        // Return 200 OK with search results.
        return Ok( result );
    }

    /// <summary>
    /// Generate password reset token for a user (admin force reset).
    /// </summary>
    [HttpPost( "users/{userId}/force-password-reset" )]
    public async Task<ActionResult<object>> ForcePasswordReset( string userId )
    {
        // Ensure the user exists before generating a token.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found" );

        // Generate a password reset token via Identity.
        var token = await _userManager.GeneratePasswordResetTokenAsync( user );

        // Log action for audit purposes.
        _logger.LogInformation( "Admin generated password reset token for user {UserId}" , userId );

        // Return token to the caller (admin should transmit securely).
        return Ok( new
        {
            userId = user.Id ,
            resetToken = token
        } );
    }

    /// <summary>
    /// Disable user account (lockout).
    /// </summary>
    [HttpPost( "users/{userId}/disable" )]
    public async Task<IActionResult> DisableUser( string userId )
    {
        // Find the user and return 404 if not present.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found" );

        // Set lockout end date to effectively disable login.
        var result = await _userManager.SetLockoutEndDateAsync( user , DateTimeOffset.MaxValue );
        if ( !result.Succeeded )
        {
            return BadRequest( result.Errors.Select( e => e.Description ) );
        }

        // Log and return NoContent to indicate success.
        _logger.LogInformation( "Admin disabled user {UserId}" , userId );
        return NoContent();
    }

    /// <summary>
    /// Enable user account (remove lockout).
    /// </summary>
    [HttpPost( "users/{userId}/enable" )]
    public async Task<IActionResult> EnableUser( string userId )
    {
        // Find the user and return 404 if not present.
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found" );

        // Clear lockout to allow the user to login again.
        var result = await _userManager.SetLockoutEndDateAsync( user , null );
        if ( !result.Succeeded )
        {
            return BadRequest( result.Errors.Select( e => e.Description ) );
        }

        // Log and return NoContent.
        _logger.LogInformation( "Admin enabled user {UserId}" , userId );
        return NoContent();
    }

    /// <summary>
    /// Generates a short-lived JWT so an admin can view the platform as a specific student.
    /// Returns no refresh token — the session is temporary and cannot be silently extended.
    /// </summary>
    [HttpPost( "users/{userId}/impersonate" )]
    public async Task<ActionResult<AuthResponseDto>> ImpersonateUser( string userId )
    {
        var target = await _userManager.FindByIdAsync( userId );
        if ( target is null )
            return NotFound( "User not found." );

        var targetRoles = await _userManager.GetRolesAsync( target );
        if ( !targetRoles.Contains( "Student" ) )
            return BadRequest( "Only Student accounts can be impersonated." );

        var adminId = User.FindFirstValue( ClaimTypes.NameIdentifier )!;

        var (token , expiration) = await _tokenService.GenerateImpersonationTokenAsync( target , adminId );

        _logger.LogInformation( "Admin {AdminId} started impersonating user {UserId}" , adminId , userId );

        return Ok( new AuthResponseDto
        {
            Success = true ,
            Token = token ,
            Expiration = expiration
        } );
    }

    /// <summary>
    /// Directly set a new password for any user (admin override, no token email required).
    /// </summary>
    [HttpPost( "users/{userId}/set-password" )]
    public async Task<IActionResult> SetPassword( string userId , [FromBody] AdminSetPasswordDto dto )
    {
        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( "User not found." );

        if ( await _userManager.IsInRoleAsync( user , "Admin" ) )
            return BadRequest( "Admin account passwords cannot be changed from the user table." );

        var token = await _userManager.GeneratePasswordResetTokenAsync( user );
        var result = await _userManager.ResetPasswordAsync( user , token , dto.NewPassword );

        if ( !result.Succeeded )
            return BadRequest( result.Errors.Select( e => e.Description ) );

        _logger.LogInformation( "Admin set new password for user {UserId}" , userId );
        return Ok();
    }

    // ─── Blog Article Management ────────────────────────────────────────────

    [HttpGet( "blog" )]
    public async Task<ActionResult<PagedResult<AdminBlogArticleDto>>> GetBlogArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default )
    {
        var result = await _blogService.GetAllForAdminAsync( page, pageSize, search, cancellationToken );
        return Ok( result );
    }

    [HttpGet( "blog/{id:guid}" )]
    public async Task<ActionResult<AdminBlogArticleDto>> GetBlogArticle(
        Guid id, CancellationToken cancellationToken = default )
    {
        var article = await _blogService.GetByIdForAdminAsync( id, cancellationToken );
        return article is null ? NotFound() : Ok( article );
    }

    [HttpPost( "blog" )]
    public async Task<ActionResult<AdminBlogArticleDto>> CreateBlogArticle(
        [FromBody] SaveBlogArticleRequest request,
        CancellationToken cancellationToken = default )
    {
        var result = await _blogService.CreateAsync( request, cancellationToken );
        if ( result is null )
            return Conflict( "A blog article with this slug already exists." );

        return CreatedAtAction( nameof( GetBlogArticle ), new { id = result.Id }, result );
    }

    [HttpPut( "blog/{id:guid}" )]
    public async Task<ActionResult<AdminBlogArticleDto>> UpdateBlogArticle(
        Guid id,
        [FromBody] SaveBlogArticleRequest request,
        CancellationToken cancellationToken = default )
    {
        var result = await _blogService.UpdateAsync( id, request, cancellationToken );
        if ( result is null )
        {
            var exists = await _blogService.GetByIdForAdminAsync( id, cancellationToken );
            return exists is null ? NotFound() : Conflict( "A blog article with this slug already exists." );
        }
        return Ok( result );
    }

    [HttpPost( "blog/{id:guid}/publish" )]
    public async Task<IActionResult> PublishBlogArticle(
        Guid id, CancellationToken cancellationToken = default )
    {
        var success = await _blogService.PublishAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }

    [HttpPost( "blog/{id:guid}/unpublish" )]
    public async Task<IActionResult> UnpublishBlogArticle(
        Guid id, CancellationToken cancellationToken = default )
    {
        var success = await _blogService.UnpublishAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }

    [HttpDelete( "blog/{id:guid}" )]
    public async Task<IActionResult> DeleteBlogArticle(
        Guid id, CancellationToken cancellationToken = default )
    {
        var success = await _blogService.DeleteAsync( id, cancellationToken );
        return success ? NoContent() : NotFound();
    }

    [HttpPost( "blog/{id:guid}/upload/cover" )]
    public async Task<IActionResult> UploadBlogCoverImage(
        Guid id, IFormFile file, CancellationToken cancellationToken = default )
    {
        if ( file is null || file.Length == 0 )
            return BadRequest( "No file provided." );

        var article = await _context.BlogArticles.FindAsync( new object[] { id }, cancellationToken );
        if ( article is null )
            return NotFound();

        // Delete the old cover image if it was an uploaded file (not a URL).
        if ( !string.IsNullOrEmpty( article.CoverImageUrl ) &&
             !article.CoverImageUrl.StartsWith( "http", StringComparison.OrdinalIgnoreCase ) )
        {
            await _fileStorage.DeleteFileAsync( article.CoverImageUrl );
        }

        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync( stream, file.FileName, "blog/covers", cancellationToken );

        article.CoverImageUrl = path;
        article.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync( cancellationToken );

        return Ok( new { coverImageUrl = path } );
    }
}
