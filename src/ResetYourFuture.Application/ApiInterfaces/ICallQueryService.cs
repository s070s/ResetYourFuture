using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Call access checks and callable-user lookups.
/// </summary>
public interface ICallQueryService
{
    Task<bool> HasCallAccessAsync(string userId, bool isAdmin);
    Task<List<ChatUserDto>> GetCallableUsersAsync(string callerId, string? search);
}
