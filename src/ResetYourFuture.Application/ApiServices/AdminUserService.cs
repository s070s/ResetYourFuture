using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Admin-only user and role management.
/// </summary>
public class AdminUserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ITokenService tokenService,
    ILogger<AdminUserService> logger,
    IApplicationDbContext context,
    IEmailService emailService) : IAdminUserService
{
    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(
        int page, int pageSize, string? search, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        var query = userManager.Users.AsNoTracking();

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
        var userRolePairs = await context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(context.Roles,
                   ur => ur.RoleId,
                   r => r.Id,
                   (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var userRoleMap = userRolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        // Single query: active subscription tier per user; users without one are Free
        var userTierPairs = await context.UserSubscriptions
            .Where(us => userIds.Contains(us.UserId) && us.IsActive)
            .Select(us => new { us.UserId, us.SubscriptionPlan.Tier })
            .ToListAsync(cancellationToken);

        var userTierMap = userTierPairs.ToDictionary(x => x.UserId, x => x.Tier);

        var result = users.Select(user =>
        {
            var roles = userRoleMap.TryGetValue(user.Id, out var r) ? r : [];
            var tier = userTierMap.TryGetValue(user.Id, out var t) ? t : SubscriptionTier.Free;
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
                tier.ToString(),
                user.CreatedAt,
                user.LastSeenAt
            );
        }).ToList();

        return new PagedResult<AdminUserDto>(result, totalCount, page, pageSize, sortBy, sortDir);
    }

    public async Task<AdminUserDetailDto?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        var roles = await userManager.GetRolesAsync(user);
        return new AdminUserDetailDto(
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
            [.. roles]);
    }

    public async Task<ServiceResult<string>> AssignRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<string>.NotFound(error: "User not found.");

        if (!await roleManager.RoleExistsAsync(roleName))
            return ServiceResult<string>.BadRequest(error: $"Role '{roleName}' does not exist.");

        if (await userManager.IsInRoleAsync(user, roleName))
            return ServiceResult<string>.BadRequest(error: $"User already has role '{roleName}'.");

        var result = await userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
            return ServiceResult<string>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin assigned role {Role} to user {UserId}", roleName, userId);
        return ServiceResult<string>.Ok($"Role '{roleName}' assigned.");
    }

    public async Task<ServiceResult<string>> RemoveRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<string>.NotFound(error: "User not found.");

        if (!await userManager.IsInRoleAsync(user, roleName))
            return ServiceResult<string>.BadRequest(error: $"User does not have role '{roleName}'.");

        var result = await userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
            return ServiceResult<string>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin removed role {Role} from user {UserId}", roleName, userId);
        return ServiceResult<string>.Ok($"Role '{roleName}' removed.");
    }

    public async Task<List<string?>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await roleManager.Roles.Select(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<string>> CreateRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (await roleManager.RoleExistsAsync(roleName))
            return ServiceResult<string>.BadRequest(error: $"Role '{roleName}' already exists.");

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
            return ServiceResult<string>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin created role {Role}", roleName);
        return ServiceResult<string>.Ok($"Role '{roleName}' created.");
    }

    public async Task<ServiceResult<UserEnabledStateDto>> ToggleEnableAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<UserEnabledStateDto>.NotFound(error: "User not found.");

        if (await userManager.IsInRoleAsync(user, "Admin"))
            return ServiceResult<UserEnabledStateDto>.BadRequest(error: "Admin accounts cannot be disabled.");

        user.IsEnabled = !user.IsEnabled;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return ServiceResult<UserEnabledStateDto>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin toggled IsEnabled to {IsEnabled} for user {UserId}", user.IsEnabled, userId);
        return ServiceResult<UserEnabledStateDto>.Ok(new UserEnabledStateDto(user.IsEnabled));
    }

    public async Task<ServiceResult<string>> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<string>.NotFound(error: "User not found.");

        if (await userManager.IsInRoleAsync(user, "Admin"))
            return ServiceResult<string>.BadRequest(error: "Admin accounts cannot be deleted.");

        // Chat and call history rows reference the user with Restrict FKs, so a bare
        // Identity delete throws for any user who ever chatted or joined a call.
        // Stage the history cleanup in the change tracker; userManager.DeleteAsync shares
        // this scoped DbContext, so its SaveChanges flushes the cleanup and the user row
        // in a single transaction. Remaining user data (subscriptions, tokens, submissions,
        // lesson completions) is removed by the database cascades.
        var conversations = await context.ChatConversations
            .Where(c => c.CreatorId == userId || c.ParticipantId == userId)
            .ToListAsync(cancellationToken);
        var conversationIds = conversations.Select(c => c.Id).ToList();

        // Messages the user sent anywhere, plus every message inside the conversations
        // being removed (the other party's messages go with their conversation).
        var messages = await context.ChatMessages
            .Where(m => m.SenderId == userId || conversationIds.Contains(m.ConversationId))
            .ToListAsync(cancellationToken);

        var initiatedSessions = await context.CallSessions
            .Where(s => s.InitiatorId == userId)
            .ToListAsync(cancellationToken);
        var initiatedSessionIds = initiatedSessions.Select(s => s.Id).ToList();

        var callParticipants = await context.CallParticipants
            .Where(p => p.UserId == userId || initiatedSessionIds.Contains(p.CallSessionId))
            .ToListAsync(cancellationToken);

        // Certificates must be deleted before their enrollments: Certificate→Enrollment is
        // NoAction, so the User→Enrollment cascade alone can trip over surviving certificate
        // rows (cascade ordering across NoAction FKs is not guaranteed on SQL Server).
        var certificates = await context.Certificates
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        var enrollments = await context.Enrollments
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        context.ChatMessages.RemoveRange(messages);
        context.ChatConversations.RemoveRange(conversations);
        context.CallParticipants.RemoveRange(callParticipants);
        context.CallSessions.RemoveRange(initiatedSessions);
        context.Certificates.RemoveRange(certificates);
        context.Enrollments.RemoveRange(enrollments);

        try
        {
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return ServiceResult<string>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Deleting user {UserId} failed on a database constraint", userId);
            return ServiceResult<string>.Conflict(error: "User could not be deleted because other data still references the account.");
        }

        logger.LogInformation("Admin deleted user {UserId} and associated chat/call history", userId);
        return ServiceResult<string>.Ok("User deleted.");
    }

    public async Task<IEnumerable<AdminUserSearchResultDto>> SearchUsersAsync(string query, CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .ApplySearch(query.Trim())
            .Take(50)
            .ToListAsync(cancellationToken);

        // Single JOIN query for all roles — avoids N+1 round-trips.
        var userIds = users.Select(u => u.Id).ToList();
        var userRolePairs = await context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(context.Roles,
                   ur => ur.RoleId,
                   r => r.Id,
                   (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var userRoleMap = userRolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        return users.Select(user => new AdminUserSearchResultDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.EmailConfirmed,
            userRoleMap.TryGetValue(user.Id, out var r) ? r : new List<string>()
        ));
    }

    public async Task<ServiceResult<bool>> ForcePasswordResetAsync(
        string userId, Func<string, string, string> buildResetUrl, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<bool>.NotFound(error: "User not found");

        if (string.IsNullOrEmpty(user.Email))
            return ServiceResult<bool>.BadRequest(error: "User has no email address.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = buildResetUrl(user.Email, token);

        await emailService.SendPasswordResetAsync(user.Email, resetUrl);
        logger.LogInformation("Admin triggered forced password reset for user {UserId}.", userId);

        return ServiceResult<bool>.NoContent();
    }

    public async Task<ServiceResult<bool>> DisableUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<bool>.NotFound(error: "User not found");

        var result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded)
            return ServiceResult<bool>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin disabled user {UserId}", userId);
        return ServiceResult<bool>.NoContent();
    }

    public async Task<ServiceResult<bool>> EnableUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<bool>.NotFound(error: "User not found");

        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
            return ServiceResult<bool>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin enabled user {UserId}", userId);
        return ServiceResult<bool>.NoContent();
    }

    public async Task<ServiceResult<AuthResponseDto>> ImpersonateUserAsync(string userId, string adminId, CancellationToken cancellationToken = default)
    {
        var target = await userManager.FindByIdAsync(userId);
        if (target is null)
            return ServiceResult<AuthResponseDto>.NotFound(error: "User not found.");

        var targetRoles = await userManager.GetRolesAsync(target);
        if (!targetRoles.Contains("Student"))
            return ServiceResult<AuthResponseDto>.BadRequest(error: "Only Student accounts can be impersonated.");

        var (token, expiration) = await tokenService.GenerateImpersonationTokenAsync(target, adminId);

        logger.LogInformation("Admin {AdminId} started impersonating user {UserId}", adminId, userId);

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Success = true,
            Token = token,
            Expiration = expiration
        });
    }

    public async Task<ServiceResult<bool>> SetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return ServiceResult<bool>.NotFound(error: "User not found.");

        if (await userManager.IsInRoleAsync(user, "Admin"))
            return ServiceResult<bool>.BadRequest(error: "Admin account passwords cannot be changed from the user table.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
            return ServiceResult<bool>.BadRequest(error: string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("Admin set new password for user {UserId}", userId);
        return ServiceResult<bool>.Ok(true);
    }
}
