using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// HTTP consumer for the admin user management API.
/// </summary>
public class AdminUserConsumer : IAdminUserConsumer
{
    private readonly HttpClient _http;

    public AdminUserConsumer( HttpClient http )
    {
        _http = http;
    }

    public async Task<PagedResult<AdminUserDto>?> GetUsersAsync( int page = 1 , int pageSize = 10 , string? search = null , string sortBy = "email" , string sortDir = "asc" )
    {
        var url = $"api/admin/users?page={page}&pageSize={pageSize}" +
                  $"&sortBy={Uri.EscapeDataString( sortBy )}" +
                  $"&sortDir={Uri.EscapeDataString( sortDir )}";
        if ( !string.IsNullOrWhiteSpace( search ) )
            url += $"&search={Uri.EscapeDataString( search )}";

        var response = await _http.GetAsync( url );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PagedResult<AdminUserDto>>()
            : null;
    }

    public async Task<AdminUserDto?> GetUserAsync( string userId )
    {
        var response = await _http.GetAsync( $"api/admin/users/{userId}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminUserDto>()
            : null;
    }

    public async Task<bool?> ToggleEnableAsync( string userId )
    {
        var response = await _http.PostAsync( $"api/admin/users/{userId}/toggle-enable" , null );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<ToggleEnableResponse>();
        return result?.IsEnabled;
    }

    public async Task<bool> DeleteUserAsync( string userId )
    {
        var response = await _http.DeleteAsync( $"api/admin/users/{userId}" );
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> ForcePasswordResetAsync( string userId )
    {
        var response = await _http.PostAsync( $"api/admin/users/{userId}/force-password-reset" , null );
        if ( !response.IsSuccessStatusCode )
            return null;

        var result = await response.Content.ReadFromJsonAsync<ForcePasswordResetResponse>();
        return result?.ResetToken;
    }

    public async Task<bool> DisableUserAsync( string userId )
    {
        var response = await _http.PostAsync( $"api/admin/users/{userId}/disable" , null );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetPasswordAsync( string userId , string newPassword )
    {
        var response = await _http.PostAsJsonAsync(
            $"api/admin/users/{userId}/set-password" ,
            new AdminSetPasswordDto { NewPassword = newPassword } );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> EnableUserAsync( string userId )
    {
        var response = await _http.PostAsync( $"api/admin/users/{userId}/enable" , null );
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AssignRoleAsync( string userId , string roleName )
    {
        var response = await _http.PostAsync( $"api/admin/users/{userId}/roles/{roleName}" , null );
        return response.IsSuccessStatusCode;
    }

    public async Task<List<AdminUserDto>> SearchUsersAsync( string query )
    {
        var response = await _http.GetAsync( $"api/admin/users/search?query={Uri.EscapeDataString( query )}" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<AdminUserDto>>() ?? []
            : [];
    }

    public async Task<bool> RemoveRoleAsync( string userId , string roleName )
    {
        var response = await _http.DeleteAsync( $"api/admin/users/{userId}/roles/{roleName}" );
        return response.IsSuccessStatusCode;
    }

    public async Task<List<string>> GetRolesAsync()
    {
        var response = await _http.GetAsync( "api/admin/roles" );
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<string>>() ?? []
            : [];
    }

    private record ToggleEnableResponse( bool IsEnabled );
    private record ForcePasswordResetResponse( string UserId , string ResetToken );
}
