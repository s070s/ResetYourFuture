using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Interfaces;

namespace ResetYourFuture.Web.Shared.Components.Call;

public partial class CallUserPickerModal : IDisposable
{
    [Inject] private ICallService CallService { get; set; } = default!;

    [Parameter, EditorRequired] public bool IsVisible { get; set; }
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter, EditorRequired] public string ConfirmLabel { get; set; } = "";
    [Parameter] public int MaxSelectable { get; set; } = 6;
    [Parameter, EditorRequired] public EventCallback OnClose { get; set; }
    [Parameter, EditorRequired] public EventCallback<List<ChatUserDto>> OnConfirm { get; set; }

    private string _searchTerm = string.Empty;
    private List<ChatUserDto> _availableUsers = [];
    private readonly Dictionary<string, ChatUserDto> _selected = [];
    private bool _isSearching;
    private bool _wasVisible;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnParametersSetAsync()
    {
        var justOpened = IsVisible && !_wasVisible;
        _wasVisible = IsVisible;

        if (justOpened)
        {
            _searchTerm = string.Empty;
            _availableUsers = [];
            _selected.Clear();
            await SearchUsersAsync();
        }
    }

    private async Task HandleSearchKeyDown(KeyboardEventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(300, token);
            await SearchUsersAsync();
        }
        catch (TaskCanceledException)
        {
            // Expected when typing quickly.
        }
    }

    private async Task SearchUsersAsync()
    {
        _isSearching = true;
        StateHasChanged();

        _availableUsers = await CallService.GetCallableUsersAsync(_searchTerm);

        _isSearching = false;
        StateHasChanged();
    }

    private void ToggleUser(ChatUserDto user, bool atLimit)
    {
        if (_selected.ContainsKey(user.Id))
        {
            _selected.Remove(user.Id);
        }
        else if (!atLimit)
        {
            _selected[user.Id] = user;
        }
    }

    private async Task Close() => await OnClose.InvokeAsync();

    private async Task Confirm()
    {
        var picked = _selected.Values.ToList();
        _selected.Clear();
        await OnConfirm.InvokeAsync(picked);
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
