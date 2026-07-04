using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.ApiInterfaces;
using System.Security.Claims;

namespace ResetYourFuture.Web.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Authentication")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class AuthController : ControllerBase
{
    private readonly IAuthApiService _authApiService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthApiService authApiService, IWebHostEnvironment env)
    {
        _authApiService = authApiService;
        _env = env;
    }

    /// <summary>
    /// Register a new user. Assigns Student role by default.
    /// Email confirmation is required before login.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponseDto { Success = false, Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var result = await _authApiService.RegisterAsync(request, (userId, token) =>
            Url.Action("ConfirmEmail", "Auth", new { userId, token }, Request.Scheme));
        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Confirm user email address.
    /// </summary>
    [HttpGet("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        var result = await _authApiService.ConfirmEmailAsync(userId, token);
        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Login with email and password. Returns JWT access token.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponseDto { Success = false, Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var result = await _authApiService.LoginAsync(request);
        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Exchange a valid refresh token for a new JWT access token and rotated refresh token.
    /// The submitted refresh token is revoked; a fresh pair is returned.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponseDto { Success = false, Message = "Refresh token is required." });

        var result = await _authApiService.RefreshAsync(request);
        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Request password reset. Returns token (dev mode). In production, send via email.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var dto = await _authApiService.ForgotPasswordAsync(request, (email, token) =>
            $"{Request.Scheme}://{Request.Host}/reset-password?email={email}&token={Uri.EscapeDataString(token)}");
        return Ok(dto);
    }

    /// <summary>
    /// Reset password using token from forgot-password flow.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponseDto { Success = false, Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var result = await _authApiService.ResetPasswordAsync(request);
        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Get current user info. Requires authentication.
    /// </summary>
    /// <response code="200">The authenticated user's identity summary.</response>
    /// <response code="404">The user in the token no longer exists.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized();

        var dto = await _authApiService.GetCurrentUserAsync(userId);
        return dto is not null ? Ok(dto) : NotFound();
    }

    // NOTE: these dev-only endpoints back
    // the self-confirm / self-reset buttons on the Register/Login/ForgotPassword pages, which is
    // why those flows complete in Development despite no email being delivered. They are compiled
    // out of Release builds (#if DEBUG) and additionally gated by IsDevelopment() at runtime, so
    // they provide no fallback in production — the real email flows (AUTH-1/AUTH-2) must be wired.
#if DEBUG
    /// <summary>
    /// Dev-only: Confirm email without token (development mode only).
    /// </summary>
    [HttpPost("dev/confirm-email")]
    public async Task<ActionResult<AuthResponseDto>> DevConfirmEmail([FromBody] string email)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var result = await _authApiService.DevConfirmEmailAsync(email);
        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Dev-only: Reset password without token (development mode only).
    /// </summary>
    [HttpPost("dev/reset-password")]
    public async Task<ActionResult<AuthResponseDto>> DevResetPassword([FromBody] DevResetPasswordRequestDto request)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var result = await _authApiService.DevResetPasswordAsync(request);
        return StatusCode(result.StatusCode, result.Value);
    }
#endif
}
