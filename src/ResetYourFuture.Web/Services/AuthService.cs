using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Identity;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// SSR-compatible auth service.
/// Authentication state is maintained via HttpOnly cookies rather than localStorage.
///
/// IMPORTANT — Blazor Server circuit constraint:
/// HttpContext.SignInAsync / SignOutAsync write Set-Cookie response headers, which is
/// impossible once the Blazor circuit has taken over the connection (the HTTP response
/// has already been committed to the browser).
///
/// To work around this, all cookie-writing operations are deferred.  The Blazor
/// component calls LoginAsync / ImpersonateAsync / ExitImpersonationAsync, receives a
/// redirect URL containing a short-lived signed DataProtection token, then issues a
/// forceLoad navigation.  The /auth/complete minimal endpoint is invoked on a fresh HTTP
/// request (response not yet started), decodes the token, performs the DB lookup to
/// rebuild the principal, calls SignInAsync, and redirects the browser.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly string _jwtKey;
    private readonly string? _jwtIssuer;
    private readonly string? _jwtAudience;
    private readonly double _jwtExpirationMinutes;
    private readonly ITimeLimitedDataProtector _protector;

    /// <summary>Cookie name used to store the admin's user ID during impersonation.</summary>
    internal const string AdminBackupCookieName = ".RYF.AdminUserId";

    /// <summary>Data Protection purpose string — changing this invalidates all in-flight tokens.</summary>
    internal const string ProtectorPurpose = "ResetYourFuture.AuthCompletion.v1";

    private static readonly JwtSecurityTokenHandler TokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    public AuthService(
        IHttpContextAccessor httpContextAccessor ,
        UserManager<ApplicationUser> userManager ,
        SignInManager<ApplicationUser> signInManager ,
        ISubscriptionService subscriptionService ,
        ApplicationDbContext context ,
        IDataProtectionProvider dataProtectionProvider ,
        IConfiguration config ,
        ILogger<AuthService> logger )
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _signInManager = signInManager;
        _subscriptionService = subscriptionService;
        _context = context;
        _logger = logger;
        _jwtKey = config [ "Jwt:Key" ] ?? throw new InvalidOperationException( "Jwt:Key not configured" );
        _jwtIssuer = config [ "Jwt:Issuer" ];
        _jwtAudience = config [ "Jwt:Audience" ];
        _jwtExpirationMinutes = double.Parse( config [ "Jwt:AccessTokenExpirationMinutes" ] ?? "60" );
        _protector = dataProtectionProvider
            .CreateProtector( ProtectorPurpose )
            .ToTimeLimitedDataProtector();
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException( "HttpContext is not available." );

    /// <inheritdoc />
    /// <remarks>
    /// On success, <see cref="AuthResponseDto.Token"/> contains a signed DataProtection
    /// token.  Navigate to <c>/auth/complete?ticket={Token}&amp;returnUrl=…</c>
    /// with <c>forceLoad: true</c> to complete cookie issuance on a fresh HTTP request.
    /// </remarks>
    public async Task<AuthResponseDto> LoginAsync( LoginRequestDto request )
    {
        // Clear the EF Core change tracker so this call always reads fresh data from the
        // database.  In Blazor Server the DbContext is scoped to the circuit lifetime
        // (long-lived), so without this a previously-loaded entity (e.g. from a failed
        // login attempt before the email was confirmed) would be returned stale from the
        // identity map and IsEmailConfirmedAsync would still see the old false value.
        _context.ChangeTracker.Clear();

        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user is null )
        {
            _logger.LogWarning( "Login attempt for non-existent user: {Email}" , request.Email );
            return new AuthResponseDto { Success = false , Message = "Invalid credentials." };
        }

        if ( !await _userManager.IsEmailConfirmedAsync( user ) )
            return new AuthResponseDto { Success = false , Message = "Email not confirmed." };

        if ( !user.IsEnabled )
        {
            _logger.LogWarning( "Login blocked for disabled user: {Email}" , request.Email );
            return new AuthResponseDto { Success = false , Message = "Your account has been disabled. Please contact support." };
        }

        var result = await _signInManager.CheckPasswordSignInAsync( user , request.Password , lockoutOnFailure: true );
        if ( !result.Succeeded )
        {
            if ( result.IsLockedOut )
                return new AuthResponseDto { Success = false , Message = "Account locked. Try again later." };
            return new AuthResponseDto { Success = false , Message = "Invalid credentials." };
        }

        var token = CreateSignInToken( userId: user.Id , adminBackupId: null , deleteAdminBackup: false );
        _logger.LogInformation( "User {Email} validated — sign-in token issued." , user.Email );
        return new AuthResponseDto { Success = true , Token = token };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the URL the caller should navigate to (with forceLoad: true) to actually
    /// clear the auth cookie on a fresh HTTP request.
    /// </remarks>
    public Task<string> LogoutAsync()
    {
        return Task.FromResult( "/auth/signout?returnUrl=%2F" );
    }

    public async Task<AuthResponseDto> RegisterAsync( RegisterRequestDto request )
    {
        DateOnly? dob = request.DateOfBirth.HasValue
            ? DateOnly.FromDateTime( request.DateOfBirth.Value.Date )
            : null;

        var user = new ApplicationUser
        {
            UserName = request.Email ,
            Email = request.Email ,
            FirstName = request.FirstName ,
            LastName = request.LastName ,
            DateOfBirth = dob ,
            Status = UserStatus.Student ,
            GdprConsentGiven = request.GdprConsent ,
            GdprConsentDate = request.GdprConsent ? DateTime.UtcNow : null
        };

        var createResult = await _userManager.CreateAsync( user , request.Password );
        if ( !createResult.Succeeded )
        {
            _logger.LogWarning( "Registration failed for {Email}: {Errors}" ,
                request.Email , string.Join( ", " , createResult.Errors.Select( e => e.Description ) ) );
            return new AuthResponseDto
            {
                Success = false ,
                Errors = createResult.Errors.Select( e => e.Description )
            };
        }

        await _userManager.AddToRoleAsync( user , "Student" );
        await _subscriptionService.AssignFreePlanAsync( user.Id );

        _ = await _userManager.GenerateEmailConfirmationTokenAsync( user );
        _logger.LogInformation( "User {Email} registered." , request.Email );

        return new AuthResponseDto
        {
            Success = true ,
            Message = "Registration successful. Please confirm your email."
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// On success, <see cref="AuthResponseDto.Token"/> contains a signed DataProtection
    /// token.  Navigate to <c>/auth/complete?ticket={Token}&amp;returnUrl=…</c>
    /// with <c>forceLoad: true</c>.
    /// </remarks>
    public async Task<AuthResponseDto> ImpersonateAsync( string userId )
    {
        var target = await _userManager.FindByIdAsync( userId );
        if ( target is null )
            return new AuthResponseDto { Success = false , Message = "User not found." };

        var targetRoles = await _userManager.GetRolesAsync( target );
        if ( !targetRoles.Contains( "Student" ) )
            return new AuthResponseDto { Success = false , Message = "Only Student accounts can be impersonated." };

        var adminId = HttpContext.User.FindFirstValue( ClaimTypes.NameIdentifier );
        if ( string.IsNullOrEmpty( adminId ) )
            return new AuthResponseDto { Success = false , Message = "Not authenticated." };

        var token = CreateSignInToken( userId: target.Id , adminBackupId: adminId , deleteAdminBackup: false );
        _logger.LogInformation( "Admin {AdminId} issued impersonation token for user {UserId}." , adminId , userId );
        return new AuthResponseDto { Success = true , Token = token };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the URL to navigate to (with forceLoad: true) to restore the admin session.
    /// </remarks>
    public async Task<string> ExitImpersonationAsync()
    {
        var adminIdCookieValue = HttpContext.Request.Cookies [ AdminBackupCookieName ];
        if ( string.IsNullOrEmpty( adminIdCookieValue ) )
            return "/";

        var admin = await _userManager.FindByIdAsync( adminIdCookieValue );
        if ( admin is null )
        {
            _logger.LogWarning( "ExitImpersonation: admin backup cookie held an invalid user ID — signing out." );
            return "/auth/signout?returnUrl=%2F";
        }

        var token = CreateSignInToken( userId: admin.Id , adminBackupId: null , deleteAdminBackup: true );
        _logger.LogInformation( "Admin {AdminId} exiting impersonation — token issued." , admin.Id );
        return $"/auth/complete?ticket={Uri.EscapeDataString( token )}&returnUrl=%2Fadmin%2Fusers";
    }

    public Task<bool> IsImpersonatingAsync()
    {
        var value = HttpContext.Request.Cookies [ AdminBackupCookieName ];
        return Task.FromResult( !string.IsNullOrEmpty( value ) );
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult( HttpContext.User.Identity?.IsAuthenticated == true );
    }

    /// <summary>
    /// Generates a short-lived JWT from HttpContext.User claims.
    /// Only safe to call during HTTP requests (SSR render, API controllers).
    /// Use <see cref="GetTokenAsync(ClaimsPrincipal)"/> inside Blazor Server circuits.
    /// </summary>
    public Task<string?> GetTokenAsync() => GetTokenAsync( HttpContext.User );

    /// <summary>
    /// Generates a short-lived JWT from the supplied principal — no HttpContext needed.
    /// Safe to call inside Blazor Server circuits; pass AuthenticationState.User.
    /// </summary>
    public Task<string?> GetTokenAsync( ClaimsPrincipal principal )
    {
        if ( principal.Identity?.IsAuthenticated != true )
            return Task.FromResult<string?>( null );

        var key = new SymmetricSecurityKey( Encoding.UTF8.GetBytes( _jwtKey ) );
        var creds = new SigningCredentials( key , SecurityAlgorithms.HmacSha256 );
        var jwt = new JwtSecurityToken(
            issuer: _jwtIssuer ,
            audience: _jwtAudience ,
            claims: principal.Claims ,
            expires: DateTime.UtcNow.AddMinutes( _jwtExpirationMinutes ) ,
            signingCredentials: creds );

        return Task.FromResult<string?>( TokenHandler.WriteToken( jwt ) );
    }

    public async Task<AuthResponseDto> ForgotPasswordAsync( ForgotPasswordRequestDto request )
    {
        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user is null || !await _userManager.IsEmailConfirmedAsync( user ) )
            return new AuthResponseDto { Success = true , Message = "If the email exists, a reset link has been sent." };

        _ = await _userManager.GeneratePasswordResetTokenAsync( user );
        _logger.LogInformation( "Password reset requested for {Email}." , request.Email );

        return new AuthResponseDto { Success = true , Message = "If the email exists, a reset link has been sent." };
    }

    public async Task<AuthResponseDto> ResetPasswordAsync( ResetPasswordRequestDto request )
    {
        var user = await _userManager.FindByEmailAsync( request.Email );
        if ( user is null )
            return new AuthResponseDto { Success = false , Message = "Invalid request." };

        var result = await _userManager.ResetPasswordAsync( user , request.Token , request.NewPassword );
        if ( !result.Succeeded )
            return new AuthResponseDto { Success = false , Errors = result.Errors.Select( e => e.Description ) };

        _logger.LogInformation( "Password reset for {Email}" , request.Email );
        return new AuthResponseDto { Success = true , Message = "Password reset successfully." };
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a short-lived signed DataProtection token encoding the user ID and
    /// any impersonation metadata.  The /auth/complete endpoint decodes this token,
    /// performs a DB lookup, builds the <see cref="ClaimsPrincipal"/>, and calls
    /// <c>HttpContext.SignInAsync</c> on a fresh HTTP request.
    /// </summary>
    /// <param name="userId">The ID of the user to sign in as.</param>
    /// <param name="adminBackupId">
    /// If non-null, the admin's user ID to store in the backup cookie (impersonation start).
    /// </param>
    /// <param name="deleteAdminBackup">
    /// If true, the /auth/complete endpoint will delete the admin backup cookie (impersonation exit).
    /// </param>
    private string CreateSignInToken( string userId , string? adminBackupId , bool deleteAdminBackup )
    {
        // Format: "{userId}|{adminBackupId or empty}|{0 or 1}"
        var payload = $"{userId}|{adminBackupId ?? ""}|{( deleteAdminBackup ? "1" : "0" )}";
        return _protector.Protect( payload , lifetime: TimeSpan.FromMinutes( 5 ) );
    }
}
