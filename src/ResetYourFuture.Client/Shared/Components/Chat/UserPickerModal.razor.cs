using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Shared.Components.Chat;

public partial class UserPickerModal : IDisposable
{
    [Inject] private IChatService ChatService { get; set; } = default!;

    [Parameter, EditorRequired] public bool IsVisible { get; set; }
    [Parameter, EditorRequired] public EventCallback OnClose { get; set; }
    [Parameter, EditorRequired] public EventCallback<ChatUserDto> OnUserPicked { get; set; }

    private string _searchTerm = string.Empty;
    private List<ChatUserDto> _availableUsers = [];
    private bool _isSearching;
    private bool _wasVisible;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnParametersSetAsync()
    {
        var justOpened = IsVisible && !_wasVisible;
        _wasVisible = IsVisible;

        if ( justOpened )
        {
            _searchTerm = string.Empty;
            _availableUsers = [];
            await SearchUsersAsync();
        }
    }

    private async Task HandleSearchKeyDown( KeyboardEventArgs e )
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay( 300 , token );
            await SearchUsersAsync();
        }
        catch ( TaskCanceledException )
        {
            // Expected when typing quickly.
        }
    }

    private async Task SearchUsersAsync()
    {
        _isSearching = true;
        StateHasChanged();

        _availableUsers = await ChatService.GetAvailableUsersAsync( _searchTerm );

        _isSearching = false;
        StateHasChanged();
    }

    private async Task PickUser( ChatUserDto user )
    {
        await OnUserPicked.InvokeAsync( user );
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
