using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Identity;
using ResetYourFuture.Shared.Resources.Messages;
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
            Status = request.Status == UserStatus.Unknown ? UserStatus.Student : request.Status,
            GdprConsentGiven = request.GdprConsent,
            GdprConsentDate = request.GdprConsent ? DateTime.UtcNow : null
        };

        // Parental consent placeholder: if under 18, flag for future handling
        if (user.Age.HasValue && user.Age < 18)
        {
            // TODO: Implement parental consent flow. For now, allow registration but log.
            logger.LogInformation("Under-18 user registered: {UserId}. Parental consent not yet implemented.", user.Id);
        }

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

            // Map duplicate-account errors to a generic message to prevent account enumeration.
            // Password-policy errors (too short, no digits, etc.) are safe to surface verbatim.
            var safeErrors = result.Errors.Select(e =>
                e.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? ErrorMessagesRes.RegistrationFailedGeneric
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
        //
        // REL-2: the account/role/Free-plan are already committed above — a transient SMTP
        // failure here must not turn into a 500 after the account exists (the user would be
        // stuck: can't re-register with a duplicate email, and never got the confirmation link).
        // Log and swallow instead; registration still reports success.
        logger.LogInformation("User {UserId} registered. Confirmation email queued.", user.Id);
        try
        {
            await emailService.SendEmailConfirmationAsync(user.Email!, confirmUrl!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send confirmation email for user {UserId} after registration.", user.Id);
        }

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Success = true,
            Message = SuccessMessagesRes.RegistrationSuccessfulCheckEmail
        });
    }

    public async Task<ServiceResult<AuthResponseDto>> ConfirmEmailAsync(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidConfirmationLink });

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<AuthResponseDto>.NotFound(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.UserNotFound });

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogWarning("Email confirmation failed for {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Errors = result.Errors.Select(e => e.Description) });
        }

        logger.LogInformation("Email confirmed for user {UserId}", user.Id);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = SuccessMessagesRes.EmailConfirmedSuccessfully });
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidCredentials });
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            logger.LogWarning("Login blocked for unconfirmed email: user {UserId}", user.Id);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidCredentials });
        }

        if (!user.IsEnabled)
        {
            logger.LogWarning("Login blocked for disabled user: {UserId}", user.Id);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.AccountDisabledContactSupport });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                logger.LogWarning("User {UserId} is locked out.", user.Id);
                return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.AccountLocked });
            }
            logger.LogWarning("Invalid password for user {UserId}", user.Id);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidCredentials });
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
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStampAtIssuance = user.SecurityStamp
        };

        context.RefreshTokens.Add(refreshTokenEntity);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} logged in.", user.Id);

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

        if (stored is null || stored.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            logger.LogWarning("Refresh attempt with invalid or expired token.");
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidOrExpiredRefreshToken });
        }

        // SEC-1 reuse detection: a token is only ever revoked by rotation (below) or by one of
        // the other guards in this method — so presenting an already-revoked token again means
        // it was stolen and the legitimate rotation already happened. Sever the whole descendant
        // chain rather than just this one token, since the thief may already hold a later token too.
        if (stored.RevokedAt is not null)
        {
            await RevokeTokenChainAsync(stored);
            logger.LogWarning("Refresh token reuse detected for user {UserId} — revoked the token chain.", stored.UserId);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidOrExpiredRefreshToken });
        }

        var user = stored.User;

        if (!user.IsEnabled)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
            logger.LogWarning("Refresh attempt for disabled user {UserId}.", user.Id);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.AccountIsDisabled });
        }

        // SEC-1: a password reset (self-service or admin-forced) rotates SecurityStamp but this
        // token was minted before that — reject it instead of letting a stolen token keep working
        // for its full remaining lifetime after the victim thinks they've secured their account.
        if (stored.SecurityStampAtIssuance != user.SecurityStamp)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
            logger.LogWarning("Refresh attempt with stale security stamp for user {UserId} (password changed since issuance).", user.Id);
            return ServiceResult<AuthResponseDto>.Unauthorized(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidOrExpiredRefreshToken });
        }

        // Rotate: revoke the old token and issue a new pair
        var newRefreshTokenPlain = tokenService.GenerateRefreshToken();
        var newEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(newRefreshTokenPlain),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStampAtIssuance = user.SecurityStamp
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

    /// <summary>
    /// SEC-1 reuse-detection response: walks the rotation chain forward from an already-revoked
    /// token that was just presented again, revoking every still-active descendant. Bounded by
    /// <c>MaxChainLength</c> so a corrupted/cyclic <c>ReplacedByTokenId</c> chain can't loop forever.
    /// </summary>
    private async Task RevokeTokenChainAsync(RefreshToken start)
    {
        const int maxChainLength = 50;
        var now = DateTimeOffset.UtcNow;
        var nextId = start.ReplacedByTokenId;
        var hops = 0;

        while (nextId is not null && hops++ < maxChainLength)
        {
            var next = await context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == nextId);
            if (next is null)
                break;

            if (next.RevokedAt is null)
                next.RevokedAt = now;

            nextId = next.ReplacedByTokenId;
        }

        await context.SaveChangesAsync();
    }

    public async Task<AuthResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request, Func<string, string, string> buildResetUrl)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null || !await userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal if user exists
            return new AuthResponseDto { Success = true, Message = SuccessMessagesRes.PasswordResetLinkSent };
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = buildResetUrl(user.Email!, token);

        logger.LogInformation("Password reset requested for user {UserId}. Reset email queued.", user.Id);
        await emailService.SendPasswordResetAsync(user.Email!, resetUrl);

        return new AuthResponseDto { Success = true, Message = SuccessMessagesRes.PasswordResetLinkSent };
    }

    public async Task<ServiceResult<AuthResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal if user exists
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidRequest });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Password reset failed for user {UserId}: {Errors}", user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
            // Return generic message — specific errors would confirm account existence or reveal policy hints.
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.InvalidRequest });
        }

        // SEC-1: ResetPasswordAsync rotated SecurityStamp, which RefreshAsync's stamp check will
        // now reject on its own — this bulk revoke just makes that outcome immediate/explicit
        // instead of waiting for the next refresh attempt to discover the mismatch. Tracked
        // mutation + SaveChanges (not ExecuteUpdateAsync) so this runs on every provider,
        // including the EF InMemory provider integration tests use.
        var activeTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var token in activeTokens)
            token.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation("Password reset for user {UserId}", user.Id);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = SuccessMessagesRes.PasswordResetSuccessful });
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
            return ServiceResult<AuthResponseDto>.NotFound(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.UserNotFound });

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            logger.LogWarning("DevConfirmEmail: UpdateAsync failed for user {UserId}: {Errors}", user.Id, errors);
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Message = $"Confirm failed: {errors}" });
        }

        logger.LogInformation("Email confirmed for user {UserId} (dev mode)", user.Id);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = "Email confirmed (dev mode)" });
    }

    public async Task<ServiceResult<AuthResponseDto>> DevResetPasswordAsync(DevResetPasswordRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return ServiceResult<AuthResponseDto>.NotFound(new AuthResponseDto { Success = false, Message = ErrorMessagesRes.UserNotFound });

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!result.Succeeded)
            return ServiceResult<AuthResponseDto>.BadRequest(new AuthResponseDto { Success = false, Errors = result.Errors.Select(e => e.Description) });

        var devActiveTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var token in devActiveTokens)
            token.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation("Password reset for user {UserId} (dev mode)", user.Id);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto { Success = true, Message = "Password reset (dev mode)" });
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
