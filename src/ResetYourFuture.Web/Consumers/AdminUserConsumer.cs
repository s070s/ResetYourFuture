using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin user management API.
/// </summary>
public class AdminUserConsumer( HttpClient http ) : ApiClientBase( http ), IAdminUserConsumer
{
    public Task<PagedResult<AdminUserDto>?> GetUsersAsync(
        int page = 1, int pageSize = 10, string? search = null, string sortBy = "email", string sortDir = "asc" )
    {
        var url = $"api/admin/users?page={page}&pageSize={pageSize}" +
                  $"&sortBy={Uri.EscapeDataString( sortBy )}" +
                  $"&sortDir={Uri.EscapeDataString( sortDir )}";
        if ( !string.IsNullOrWhiteSpace( search ) )
            url += $"&search={Uri.EscapeDataString( search )}";
        return GetAsync<PagedResult<AdminUserDto>>( url );
    }

    public Task<AdminUserDto?> GetUserAsync( string userId )
        => GetAsync<AdminUserDto>( $"api/admin/users/{userId}" );

    public async Task<bool?> ToggleEnableAsync( string userId )
    {
        var response = await Http.PostAsync( $"api/admin/users/{userId}/toggle-enable", null );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<ToggleEnableResponse>();
        return result?.IsEnabled;
    }

    public Task<bool> DeleteUserAsync( string userId )
        => DeleteAsync( $"api/admin/users/{userId}" );

    public async Task<string?> ForcePasswordResetAsync( string userId )
    {
        var response = await Http.PostAsync( $"api/admin/users/{userId}/force-password-reset", null );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<ForcePasswordResetResponse>();
        return result?.ResetToken;
    }

    public Task<bool> DisableUserAsync( string userId )
        => ActionAsync( $"api/admin/users/{userId}/disable" );

    public Task<bool> SetPasswordAsync( string userId, string newPassword )
        => PostJsonActionAsync(
               $"api/admin/users/{userId}/set-password",
               new AdminSetPasswordDto { NewPassword = newPassword } );

    public Task<bool> EnableUserAsync( string userId )
        => ActionAsync( $"api/admin/users/{userId}/enable" );

    public Task<bool> AssignRoleAsync( string userId, string roleName )
        => ActionAsync( $"api/admin/users/{userId}/roles/{roleName}" );

    public async Task<List<AdminUserDto>> SearchUsersAsync( string query )
        => await GetAsync<List<AdminUserDto>>( $"api/admin/users/search?query={Uri.EscapeDataString( query )}" ) ?? [];

    public Task<bool> RemoveRoleAsync( string userId, string roleName )
        => DeleteAsync( $"api/admin/users/{userId}/roles/{roleName}" );

    public async Task<List<string>> GetRolesAsync()
        => await GetAsync<List<string>>( "api/admin/roles" ) ?? [];

    private record ToggleEnableResponse( bool IsEnabled );
    private record ForcePasswordResetResponse( string UserId, string ResetToken );
}
