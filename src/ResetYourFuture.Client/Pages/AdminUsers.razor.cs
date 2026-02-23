using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminUsers
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<UserDto>? users;
    private string searchTerm = string.Empty;
    private string message = string.Empty;
    private string? confirmDeleteId;

    private IEnumerable<UserDto> filteredUsers => users?
        .Where( u => string.IsNullOrEmpty( searchTerm ) ||
                    u.Email.Contains( searchTerm , StringComparison.OrdinalIgnoreCase ) ||
                    $"{u.FirstName} {u.LastName}".Contains( searchTerm , StringComparison.OrdinalIgnoreCase ) )
        ?? Enumerable.Empty<UserDto>();

    protected override async Task OnInitializedAsync()
    {
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        try
        {
            users = await Http.GetFromJsonAsync<List<UserDto>>( "api/admin/users" );
        }
        catch ( HttpRequestException ex ) when ( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
        {
            message = "Access denied";
        }
    }

    private async Task ImpersonateUser( string userId )
    {
        var result = await AuthService.ImpersonateAsync( userId );
        if ( result.Success )
        {
            Navigation.NavigateTo( "/" , forceLoad: false );
        }
        else
        {
            message = result.Message ?? "Impersonation failed.";
        }
    }

    private async Task ForcePasswordReset( string userId )
    {
        try
        {
            var response = await Http.PostAsync( $"api/admin/users/{userId}/force-password-reset" , null );
            if ( response.IsSuccessStatusCode )
            {
                message = "Password reset initiated for user";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task ToggleEnable( string userId )
    {
        try
        {
            var response = await Http.PostAsync( $"api/admin/users/{userId}/toggle-enable" , null );
            if ( response.IsSuccessStatusCode )
            {
                await LoadUsers();
                message = "User enable/disable toggled";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteUser( string userId )
    {
        try
        {
            var response = await Http.DeleteAsync( $"api/admin/users/{userId}" );
            if ( response.IsSuccessStatusCode )
            {
                confirmDeleteId = null;
                await LoadUsers();
                message = "User deleted";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private class UserDto
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsEnabled
        {
            get; set;
        }
        public bool EmailConfirmed
        {
            get; set;
        }
        public List<string> Roles { get; set; } = new();
    }
}
