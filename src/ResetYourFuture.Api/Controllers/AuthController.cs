using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Api.Services;
using ResetYourFuture.Shared.Auth;

namespace ResetYourFuture.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user. Assigns Student role by default.
    /// Email confirmation is required before login.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponse { Success = false, Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        DateOnly? dob = null;
        if (!string.IsNullOrEmpty(request.DateOfBirth) && DateOnly.TryParse(request.DateOfBirth, out var parsed))
        {
            dob = parsed;
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = dob,
            Status = UserStatus.Student, // Default status
            GdprConsentGiven = request.GdprConsent,
            GdprConsentDate = request.GdprConsent ? DateTime.UtcNow : null
        };

        // Parental consent placeholder: if under 18, flag for future handling
        if (user.Age.HasValue && user.Age < 18)
        {
            // TODO: Implement parental consent flow. For now, allow registration but log.
            _logger.LogInformation("Under-18 user registered: {Email}. Parental consent not yet implemented.", request.Email);
        }

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new AuthResponse { Success = false, Errors = result.Errors.Select(e => e.Description) });
        }

        // Assign default role
        await _userManager.AddToRoleAsync(user, "Student");

        // Generate email confirmation token
        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = Url.Action("ConfirmEmail", "Auth", new { userId = user.Id, token = confirmToken }, Request.Scheme);

        // TODO: Send email with confirmUrl. For now, return in response (dev only).
        _logger.LogInformation("User {Email} registered. Confirmation URL: {Url}", request.Email, confirmUrl);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = $"Registration successful. Please confirm your email. (Dev: {confirmUrl})"
        });
    }

    /// <summary>
    /// Confirm user email address.
    /// </summary>
    [HttpGet("confirm-email")]
    public async Task<ActionResult<AuthResponse>> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return BadRequest(new AuthResponse { Success = false, Message = "Invalid confirmation link." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new AuthResponse { Success = false, Message = "User not found." });

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Email confirmation failed for {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new AuthResponse { Success = false, Errors = result.Errors.Select(e => e.Description) });
        }

        _logger.LogInformation("Email confirmed for user {Email}", user.Email);
        return Ok(new AuthResponse { Success = true, Message = "Email confirmed successfully." });
    }

    /// <summary>
    /// Login with email and password. Returns JWT access token.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponse { Success = false, Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid credentials." });
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Email not confirmed." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Email} is locked out.", request.Email);
                return Unauthorized(new AuthResponse { Success = false, Message = "Account locked. Try again later." });
            }
            _logger.LogWarning("Invalid password for {Email}", request.Email);
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid credentials." });
        }

        var (token, expiration) = await _tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // TODO: Store refresh token in DB associated with user for rotation/revocation
        _logger.LogInformation("User {Email} logged in.", request.Email);

        return Ok(new AuthResponse
        {
            Success = true,
            Token = token,
            RefreshToken = refreshToken,
            Expiration = expiration
        });
    }

    /// <summary>
    /// Request password reset. Returns token (dev mode). In production, send via email.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<AuthResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal if user exists
            return Ok(new AuthResponse { Success = true, Message = "If the email exists, a reset link has been sent." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?email={user.Email}&token={Uri.EscapeDataString(token)}";

        // TODO: Send email with resetUrl
        _logger.LogInformation("Password reset requested for {Email}. Reset URL: {Url}", request.Email, resetUrl);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = $"If the email exists, a reset link has been sent. (Dev: {resetUrl})"
        });
    }

    /// <summary>
    /// Reset password using token from forgot-password flow.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponse { Success = false, Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal if user exists
            return BadRequest(new AuthResponse { Success = false, Message = "Invalid request." });
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new AuthResponse { Success = false, Errors = result.Errors.Select(e => e.Description) });
        }

        _logger.LogInformation("Password reset for {Email}", request.Email);
        return Ok(new AuthResponse { Success = true, Message = "Password reset successfully." });
    }

    /// <summary>
    /// Get current user info. Requires authentication.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<object>> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Age,
            Status = user.Status.ToString(),
            Roles = roles
        });
    }
}
