using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Extensions;
using ResetYourFuture.Domain.Identity;

namespace ResetYourFuture.Application.ApiServices;

/// <summary>
/// Callable-user lookups. Video calls are available to every authenticated, enabled user.
/// </summary>
public class CallQueryService(
    IApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : ICallQueryService
{
    /// <summary>
    /// All enabled users except the caller. Unlike <c>ChatQueryService.GetAvailableUsersAsync</c>,
    /// this deliberately does NOT exclude users the caller already has a conversation with —
    /// you should be able to call anyone you can chat with.
    /// </summary>
    public async Task<List<ChatUserDto>> GetCallableUsersAsync(string callerId, string? search)
    {
        var query = userManager.Users
            .Where(u => u.Id != callerId && u.IsEnabled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.ApplySearch(search.Trim());
        }

        var users = await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Take(20)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();

        var roleData = await db.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync();

        var roleMap = roleData
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).FirstOrDefault() ?? "User");

        return users.Select(u => new ChatUserDto(
            u.Id,
            $"{u.FirstName} {u.LastName}",
            roleMap.TryGetValue(u.Id, out var role) ? role : "User")).ToList();
    }
}
