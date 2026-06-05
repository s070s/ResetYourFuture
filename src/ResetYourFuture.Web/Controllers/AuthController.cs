using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Identity;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Cryptography;
using System.Text;

namespace ResetYourFuture.Web.Controllers;

[ApiController]
[Route( "api/[controller]" )]
[Tags( "Authentication" )]
[Produces( "application/json" )]
[ProducesResponseType( StatusCodes.Status400BadRequest )]
[ProducesResponseType( StatusCodes.Status401Unauthorized )]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<AuthController> _logger;
    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailService _emailService;

    public AuthController(
        UserManager<ApplicationUser> userManager ,
        SignInManager<ApplicationUser> signInManager ,
        ITokenService tokenService ,
        ISubscriptionService subscriptionService ,
        ILogger<AuthController> logger ,
        IApplicationDbContext context ,
        IWebHostEnvironment env ,
        IEmailService emailService )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _subscriptionService = subscriptionService;
        _logger = logger;
        _context = context;
        _env = env;
        _emailService = emailService;
    }

    /// <summary>
    /// Register a new user. Assigns Student role by default.
    /// Email confirmation is required before login.
    /// </summary>
    [HttpPost( "register" )]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> Register( [FromBody] RegisterRequestDto request )
    {
        if ( !ModelState.IsValid )
            return BadRequest( new AuthResponseDto { Success = false , Errors = ModelState.Values.SelectMany( v => v.Errors ).Select( e => e.ErrorMessage ) } );

        // Map incoming DateTime? to DateOnly? used by ApplicationUser
        DateOnly? dob = null;
        if ( request.DateOfBirth.HasValue )
        {
            dob = DateOnly.FromDateTime( request.DateOfBirth.Value.Date );
        }

        var user = new ApplicationUser
        {
            UserName = request.Email ,
            Email = request.Email ,
            FirstName = request.FirstName ,
            LastName = request.LastName ,
            DateOfBirth = dob ,
            Status = UserStatus.Student , // Default status
            GdprConsentGiven = request.GdprConsent ,
            GdprConsentDate = request.GdprConsent ? DateTime.UtcNow : null
        };

        // Parental consent placeholder: if under 18, flag for future handling
        if ( user.Age.HasValue && user.Age < 18 )
        {
            // TODO: Implement parental consent flow. For now, allow registration but log.
            _logger.LogInformation( "Under-18 user registered: {Email}. Parental consent not yet implemented." , request.Email );
        }

        var result = await _userManager.CreateAsync( user , request.Password );
        if ( !result.Succeeded )
        {
            _logger.LogWarning( "Registration failed for {Email}: {Errors}" , request.Email , string.Join( ", " , result.Errors.Select( e => e.Description ) ) );

            // Map duplicate-account errors to a generic message to prevent account enumeration.
            // Password-policy errors (too short, no digits, etc.) are safe to surface verbatim.
            var safeErrors = result.Errors.Select( e =>
                e.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? "Registration failed. Please check your details and try again."
                    : e.Description );

            return BadRequest( new AuthResponseDto { Success = false , Errors = safeErrors } );
        }

        // Assign default role
        await _userManager.AddToRoleAsync( user , "Student" );

        // Assign Free subscription plan
        await _subscriptionService.AssignFreePlanAsync( user.Id );

        // Generate email confirmation token
        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync( user );
        var confirmUrl = Url.Action( "ConfirmEmail" , "Auth" , new
        {
            userId = user.Id ,
            token = confirmToken
        } , Request.Scheme );

        // TODO: Send email with confirmUrl. For now, return in response (dev only).
        _logger.LogInformation( "User {Email} registered. Confirmation email queued." , request.Email );
        await _emailService.SendEmailConfirmationAsync( user.Email! , confirmUrl! );

        return Ok( new AuthResponseDto
        {
            Success = true ,
            Message = "Registration successful. Please check your email to confirm your account."
        } );
    }

    /// <summary>
    /// Confirm user email address.
    /// </summary>
    [HttpGet( "confirm-email" )]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> ConfirmEmail( [FromQuery] string userId , [FromQuery] string token )
    {
        if ( string.IsNullOrEmpty( userId ) || string.IsNullOrEmpty( token ) )
            return BadRequest( new AuthResponseDto { Success = false , Message = "Invalid confirmation link." } );

        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound( new AuthResponseDto { Success = false , Message = "User not found." } );

        var result = await _userManager.ConfirmEmailAsync( user , token );
        if ( !result.Succeeded )
        {
            _logger.LogWarning( "Email confirmation failed for {UserId}: {Errors}" , userId , string.Join( ", " , result.Errors.Select( e => e.Description ) ) );
            return BadRequest( new AuthResponseDto { Success = false , Errors = result.Errors.Select( e => e.Description ) } );
        }

        _logger.LogInformation( "Email confirmed for user {Email}" , user.Email );
        return Ok( new AuthResponseDto { Success = true , Message = "Email confirmed successfully." } );
    }

    /// <summary>
    /// Login with email and password. Returns JWT access token.
    /// </summary>
    [HttpPost( "login" )]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> Login( [FromBody] LoginRequestDto request )
    {
        if ( !ModelState.IsValid )
            return BadRequest( new AuthResponseDto { Success = false , Errors = ModelState.Values.SelectMany( v => v.Errors ).Select( e => e.ErrorMessage ) } );

        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user == null )
        {
            _logger.LogWarning( "Login attempt for non-existent user: {Email}" , request.Email );
            return Unauthorized( new AuthResponseDto { Success = false , Message = "Invalid credentials." } );
        }

        if ( !await _userManager.IsEmailConfirmedAsync( user ) )
        {
            _logger.LogWarning( "Login blocked for unconfirmed email: {Email}" , request.Email );
            return Unauthorized( new AuthResponseDto { Success = false , Message = "Invalid credentials." } );
        }

        if ( !user.IsEnabled )
        {
            _logger.LogWarning( "Login blocked for disabled user: {Email}" , request.Email );
            return Unauthorized( new AuthResponseDto { Success = false , Message = "Your account has been disabled. Please contact support." } );
        }

        var result = await _signInManager.CheckPasswordSignInAsync( user , request.Password , lockoutOnFailure: true );
        if ( !result.Succeeded )
        {
            if ( result.IsLockedOut )
            {
                _logger.LogWarning( "User {Email} is locked out." , request.Email );
                return Unauthorized( new AuthResponseDto { Success = false , Message = "Account locked. Try again later." } );
            }
            _logger.LogWarning( "Invalid password for {Email}" , request.Email );
            return Unauthorized( new AuthResponseDto { Success = false , Message = "Invalid credentials." } );
        }

        var (token , expiration) = await _tokenService.GenerateAccessTokenAsync( user );
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Store refresh token in database
        var refreshTokenExpiration = request.RememberMe
            ? DateTimeOffset.UtcNow.AddDays( 30 )
            : DateTimeOffset.UtcNow.AddDays( 7 );

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id ,
            TokenHash = HashToken( refreshToken ) ,
            ExpiresAt = refreshTokenExpiration ,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.RefreshTokens.Add( refreshTokenEntity );
        await _context.SaveChangesAsync();

        _logger.LogInformation( "User {Email} logged in." , request.Email );

        return Ok( new AuthResponseDto
        {
            Success = true ,
            Token = token ,
            RefreshToken = refreshToken ,
            Expiration = expiration
        } );
    }

    private static string HashToken( string token )
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash( Encoding.UTF8.GetBytes( token ) );
        return Convert.ToBase64String( hash );
    }

    /// <summary>
    /// Exchange a valid refresh token for a new JWT access token and rotated refresh token.
    /// The submitted refresh token is revoked; a fresh pair is returned.
    /// </summary>
    [HttpPost( "refresh" )]
    [AllowAnonymous]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> Refresh( [FromBody] RefreshTokenRequestDto request )
    {
        if ( !ModelState.IsValid )
            return BadRequest( new AuthResponseDto { Success = false , Message = "Refresh token is required." } );

        var tokenHash = HashToken( request.RefreshToken );

        var stored = await _context.RefreshTokens
            .Include( rt => rt.User )
            .FirstOrDefaultAsync( rt => rt.TokenHash == tokenHash );

        if ( stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTimeOffset.UtcNow )
        {
            _logger.LogWarning( "Refresh attempt with invalid, expired, or revoked token." );
            return Unauthorized( new AuthResponseDto { Success = false , Message = "Invalid or expired refresh token." } );
        }

        var user = stored.User;

        if ( !user.IsEnabled )
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogWarning( "Refresh attempt for disabled user {UserId}." , user.Id );
            return Unauthorized( new AuthResponseDto { Success = false , Message = "Account is disabled." } );
        }

        // Rotate: revoke the old token and issue a new pair
        var newRefreshTokenPlain = _tokenService.GenerateRefreshToken();
        var newEntity = new RefreshToken
        {
            Id = Guid.NewGuid() ,
            UserId = user.Id ,
            TokenHash = HashToken( newRefreshTokenPlain ) ,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays( 7 ) ,
            CreatedAt = DateTimeOffset.UtcNow
        };

        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.ReplacedByTokenId = newEntity.Id;
        _context.RefreshTokens.Add( newEntity );
        await _context.SaveChangesAsync();

        var (accessToken , expiration) = await _tokenService.GenerateAccessTokenAsync( user );

        _logger.LogInformation( "Refresh token rotated for user {UserId}." , user.Id );

        return Ok( new AuthResponseDto
        {
            Success = true ,
            Token = accessToken ,
            RefreshToken = newRefreshTokenPlain ,
            Expiration = expiration
        } );
    }

    /// <summary>
    /// Request password reset. Returns token (dev mode). In production, send via email.
    /// </summary>
    [HttpPost( "forgot-password" )]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> ForgotPassword( [FromBody] ForgotPasswordRequestDto request )
    {
        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user == null || !await _userManager.IsEmailConfirmedAsync( user ) )
        {
            // Don't reveal if user exists
            return Ok( new AuthResponseDto { Success = true , Message = "If the email exists, a reset link has been sent." } );
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync( user );
        var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?email={user.Email}&token={Uri.EscapeDataString( token )}";

        // TODO: Send email with resetUrl
        _logger.LogInformation( "Password reset requested for {Email}. Reset email queued." , request.Email );
        await _emailService.SendPasswordResetAsync( user.Email! , resetUrl );

        return Ok( new AuthResponseDto
        {
            Success = true ,
            Message = "If the email exists, a reset link has been sent."
        } );
    }

    /// <summary>
    /// Reset password using token from forgot-password flow.
    /// </summary>
    [HttpPost( "reset-password" )]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> ResetPassword( [FromBody] ResetPasswordRequestDto request )
    {
        if ( !ModelState.IsValid )
            return BadRequest( new AuthResponseDto { Success = false , Errors = ModelState.Values.SelectMany( v => v.Errors ).Select( e => e.ErrorMessage ) } );

        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user == null )
        {
            // Don't reveal if user exists
            return BadRequest( new AuthResponseDto { Success = false , Message = "Invalid request." } );
        }

        var result = await _userManager.ResetPasswordAsync( user , request.Token , request.NewPassword );
        if ( !result.Succeeded )
        {
            _logger.LogWarning( "Password reset failed for {Email}: {Errors}" , request.Email , string.Join( ", " , result.Errors.Select( e => e.Description ) ) );
            // Return generic message — specific errors would confirm account existence or reveal policy hints.
            return BadRequest( new AuthResponseDto { Success = false , Message = "Invalid request." } );
        }

        _logger.LogInformation( "Password reset for {Email}" , request.Email );
        return Ok( new AuthResponseDto { Success = true , Message = "Password reset successfully." } );
    }

    /// <summary>
    /// Get current user info. Requires authentication.
    /// </summary>
    /// <response code="200">The authenticated user's identity summary.</response>
    /// <response code="404">The user in the token no longer exists.</response>
    [HttpGet( "me" )]
    [Authorize]
    [ProducesResponseType<CurrentUserDto>( StatusCodes.Status200OK )]
    [ProducesResponseType( StatusCodes.Status404NotFound )]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst( System.Security.Claims.ClaimTypes.NameIdentifier )?.Value;
        if ( userId == null )
            return Unauthorized();

        var user = await _userManager.FindByIdAsync( userId );
        if ( user == null )
            return NotFound();

        var roles = await _userManager.GetRolesAsync( user );

        return Ok( new CurrentUserDto(
            user.Id ,
            user.Email ,
            user.FirstName ,
            user.LastName ,
            user.Age ,
            user.Status.ToString() ,
            [ .. roles ] ) );
    }

#if DEBUG
    /// <summary>
    /// Dev-only: Confirm email without token (development mode only).
    /// </summary>
    [HttpPost( "dev/confirm-email" )]
    public async Task<ActionResult<AuthResponseDto>> DevConfirmEmail( [FromBody] string email )
    {
        if ( !_env.IsDevelopment() )
            return NotFound();

        var user = await _userManager.FindByEmailAsync( email );
        if ( user == null )
            return NotFound( new AuthResponseDto { Success = false , Message = "User not found." } );

        user.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync( user );
        if ( !updateResult.Succeeded )
        {
            var errors = string.Join( ", " , updateResult.Errors.Select( e => e.Description ) );
            _logger.LogWarning( "DevConfirmEmail: UpdateAsync failed for {Email}: {Errors}" , email , errors );
            return BadRequest( new AuthResponseDto { Success = false , Message = $"Confirm failed: {errors}" } );
        }

        _logger.LogInformation( "Email confirmed for {Email} (dev mode)" , email );
        return Ok( new AuthResponseDto { Success = true , Message = "Email confirmed (dev mode)" } );
    }

    /// <summary>
    /// Dev-only: Reset password without token (development mode only).
    /// </summary>
    [HttpPost( "dev/reset-password" )]
    public async Task<ActionResult<AuthResponseDto>> DevResetPassword( [FromBody] DevResetPasswordRequestDto request )
    {
        if ( !_env.IsDevelopment() )
            return NotFound();

        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user == null )
            return NotFound( new AuthResponseDto { Success = false , Message = "User not found." } );

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync( user );
        var result = await _userManager.ResetPasswordAsync( user , resetToken , request.NewPassword );

        if ( !result.Succeeded )
            return BadRequest( new AuthResponseDto { Success = false , Errors = result.Errors.Select( e => e.Description ) } );

        _logger.LogInformation( "Password reset for {Email} (dev mode)" , request.Email );
        return Ok( new AuthResponseDto { Success = true , Message = "Password reset (dev mode)" } );
    }
#endif
}
