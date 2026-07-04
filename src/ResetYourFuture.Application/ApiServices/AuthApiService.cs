using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using System.Security.Cryptography;
using System.Text;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// JSON API authentication backing <c>AuthController</c>.
/// </summary>
public class AuthApiService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    ISubscriptionService subscriptionService,
    ILogger<AuthApiService> logger,
    IApplicationDbContext context,
    IEmailService emailService) : IAuthApiService
{
    public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, Func<string, string, string?> buildConfirmUrl)
    {
        // Map incoming DateTime? to DateOnly? used by ApplicationUser
        DateOnly? dob = null;
        if (request.DateOfBirth.HasValue)
        {
            dob = DateOnly.FromDateTime(request.DateOfBirth.Value.Date);
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
            logger.LogInformation("Under-18 user registered: {Email}. Parental consent not yet implemented.", request.Email);
        }

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

            // Map duplicate-account errors to a generic message to prevent account enumeration.
            // Password-policy errors (too short, no digits, etc.) are safe to surface verbatim.
            var safeErrors = result.Errors.Select(e =>
                e.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? "Registration failed. Please check your details and try again."
                    : e.Description);

            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Errors = safeErrors });
        }

        // Assign default role
        await userManager.AddToRoleAsync(user, "Student");

        // Assign Free subscription plan
        await subscriptionService.AssignFreePlanAsync(user.Id);

        // Generate email confirmation token
        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = buildConfirmUrl(user.Id, confirmToken);

        // NOTE: confirmUrl resolves to
        // the JSON API action below (/api/auth/confirm-email), not a user-facing Blazor page,
        // and in Development StubEmailService only writes it to the log. For production, add a
        // /confirm-email page and deliver this link through a real email provider.
        logger.LogInformation("User {Email} registered. Confirmation email queued.", request.Email);
        await emailService.SendEmailConfirmationAsync(user.Email!, confirmUrl!);

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Success = true,
            Message = "Registration successful. Please check your email to confirm your account."
        });
    }

    public async Task<ServiceResult<AuthResponseDto>> ConfirmEmailAsync(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = "Invalid confirmation link." });

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<AuthResponseDto>.NotFound(new AuthResponseDto { Success = false, Message = "User not found." });

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogWarning("Email confirmation failed for {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Errors = result.Errors.Select(e => e.Description) });
        }

        logger.LogInformation("Email confirmed for user {Email}", user.Email);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = "Email confirmed successfully." });
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid credentials." });
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            logger.LogWarning("Login blocked for unconfirmed email: {Email}", request.Email);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid credentials." });
        }

        if (!user.IsEnabled)
        {
            logger.LogWarning("Login blocked for disabled user: {Email}", request.Email);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Your account has been disabled. Please contact support." });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                logger.LogWarning("User {Email} is locked out.", request.Email);
                return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Account locked. Try again later." });
            }
            logger.LogWarning("Invalid password for {Email}", request.Email);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid credentials." });
        }

        var (token, expiration) = await tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = tokenService.GenerateRefreshToken();

        // Store refresh token in database
        var refreshTokenExpiration = request.RememberMe
            ? DateTimeOffset.UtcNow.AddDays(30)
            : DateTimeOffset.UtcNow.AddDays(7);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = refreshTokenExpiration,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.RefreshTokens.Add(refreshTokenEntity);
        await context.SaveChangesAsync();

        logger.LogInformation("User {Email} logged in.", request.Email);

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Success = true,
            Token = token,
            RefreshToken = refreshToken,
            Expiration = expiration
        });
    }

    public async Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request)
    {
        var tokenHash = HashToken(request.RefreshToken);

        var stored = await context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            logger.LogWarning("Refresh attempt with invalid, expired, or revoked token.");
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid or expired refresh token." });
        }

        var user = stored.User;

        if (!user.IsEnabled)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
            logger.LogWarning("Refresh attempt for disabled user {UserId}.", user.Id);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = "Account is disabled." });
        }

        // Rotate: revoke the old token and issue a new pair
        var newRefreshTokenPlain = tokenService.GenerateRefreshToken();
        var newEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(newRefreshTokenPlain),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.ReplacedByTokenId = newEntity.Id;
        context.RefreshTokens.Add(newEntity);
        await context.SaveChangesAsync();

        var (accessToken, expiration) = await tokenService.GenerateAccessTokenAsync(user);

        logger.LogInformation("Refresh token rotated for user {UserId}.", user.Id);

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Success = true,
            Token = accessToken,
            RefreshToken = newRefreshTokenPlain,
            Expiration = expiration
        });
    }

    public async Task<AuthResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request, Func<string, string, string> buildResetUrl)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null || !await userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal if user exists
            return new AuthResponseDto { Success = true, Message = "If the email exists, a reset link has been sent." };
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = buildResetUrl(user.Email!, token);

        logger.LogInformation("Password reset requested for {Email}. Reset email queued.", request.Email);
        await emailService.SendPasswordResetAsync(user.Email!, resetUrl);

        return new AuthResponseDto { Success = true, Message = "If the email exists, a reset link has been sent." };
    }

    public async Task<ServiceResult<AuthResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal if user exists
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = "Invalid request." });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Password reset failed for {Email}: {Errors}", request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            // Return generic message — specific errors would confirm account existence or reveal policy hints.
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = "Invalid request." });
        }

        logger.LogInformation("Password reset for {Email}", request.Email);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = "Password reset successfully." });
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        var roles = await userManager.GetRolesAsync(user);

        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Age,
            user.Status.ToString(),
            [.. roles]);
    }

    public async Task<ServiceResult<AuthResponseDto>> DevConfirmEmailAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
            return ServiceResult<AuthResponseDto>.NotFound(new AuthResponseDto { Success = false, Message = "User not found." });

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            logger.LogWarning("DevConfirmEmail: UpdateAsync failed for {Email}: {Errors}", email, errors);
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = $"Confirm failed: {errors}" });
        }

        logger.LogInformation("Email confirmed for {Email} (dev mode)", email);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = "Email confirmed (dev mode)" });
    }

    public async Task<ServiceResult<AuthResponseDto>> DevResetPasswordAsync(DevResetPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return ServiceResult<AuthResponseDto>.NotFound(new AuthResponseDto { Success = false, Message = "User not found." });

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!result.Succeeded)
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Errors = result.Errors.Select(e => e.Description) });

        logger.LogInformation("Password reset for {Email} (dev mode)", request.Email);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = "Password reset (dev mode)" });
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
