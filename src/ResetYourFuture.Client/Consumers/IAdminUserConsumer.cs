using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for admin user management API operations.
/// </summary>
public interface IAdminUserConsumer
{
    Task<PagedResult<AdminUserDto>?> GetUsersAsync( int page = 1 , int pageSize = 10 , string? search = null , string sortBy = "email" , string sortDir = "asc" );
    Task<List<AdminUserDto>> SearchUsersAsync( string query );
    Task<AdminUserDto?> GetUserAsync( string userId );
    Task<bool?> ToggleEnableAsync( string userId );
    Task<bool> DeleteUserAsync( string userId );
    Task<string?> ForcePasswordResetAsync( string userId );
    Task<bool> SetPasswordAsync( string userId , string newPassword );
    Task<bool> DisableUserAsync( string userId );
    Task<bool> EnableUserAsync( string userId );
    Task<bool> AssignRoleAsync( string userId , string roleName );
    Task<bool> RemoveRoleAsync( string userId , string roleName );
    Task<List<string>> GetRolesAsync();
}
