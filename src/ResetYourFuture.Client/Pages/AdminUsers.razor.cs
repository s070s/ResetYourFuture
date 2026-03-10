using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminUsers : IAsyncDisposable
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PagedResult<AdminUserDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string searchTerm = string.Empty;
    private string message = string.Empty;
    private string? confirmDeleteId;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        try
        {
            var url = $"api/admin/users?page={currentPage}&pageSize={pageSize}";
            if ( !string.IsNullOrEmpty( searchTerm ) )
                url += $"&search={Uri.EscapeDataString( searchTerm )}";

            pagedResult = await Http.GetFromJsonAsync<PagedResult<AdminUserDto>>( url );
        }
        catch ( HttpRequestException ex ) when ( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
        {
            message = "Access denied";
        }
    }

    private async Task OnPageSizeChanged( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString(), out var size ) )
        {
            pageSize = size;
            currentPage = 1;
            await LoadUsers();
        }
    }

    private async Task OnSearchInput( ChangeEventArgs e )
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        currentPage = 1;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var cts = _searchCts;

        try
        {
            await Task.Delay( 300, cts.Token );
            await LoadUsers();
        }
        catch ( OperationCanceledException ) { }
    }

    private async Task GoToPage( int page )
    {
        currentPage = page;
        await LoadUsers();
    }

    private async Task PreviousPage()
    {
        if ( currentPage > 1 )
        {
            currentPage--;
            await LoadUsers();
        }
    }

    private async Task NextPage()
    {
        if ( pagedResult is { HasNextPage: true } )
        {
            currentPage++;
            await LoadUsers();
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
                message = "Password reset initiated for user";
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

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}

