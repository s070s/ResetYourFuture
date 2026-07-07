using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;

namespace ResetYourFuture.Web.Shared.Components.Call;

public partial class CallOverlayHost : IAsyncDisposable
{
    [Inject] private ICallService CallService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool _callAccess;
    private bool _accessChecked;
    private bool _showGroupPicker;
    private string? _errorMessage;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _accessChecked) return;

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            _accessChecked = true;
            return;
        }

        if (user.IsInRole("Admin"))
        {
            _callAccess = true;
        }
        else
        {
            var status = await SubscriptionService.GetStatusAsync();
            _callAccess = status?.Features?.PrioritySupport == true;
        }

        _accessChecked = true;

        if (!_callAccess)
        {
            StateHasChanged();
            return;
        }

        CallService.StateChanged += HandleStateChanged;
        CallService.ErrorOccurred += HandleError;
        CallService.GroupCallPickerRequested += HandleGroupCallPickerRequested;

        await CallService.EnsureConnectedAsync(user);
        StateHasChanged();
    }

    private void HandleStateChanged() => InvokeAsync(StateHasChanged);

    private void HandleError(string message)
    {
        _errorMessage = MapErrorMessage(message);
        InvokeAsync(StateHasChanged);
    }

    private void HandleGroupCallPickerRequested()
    {
        _showGroupPicker = true;
        InvokeAsync(StateHasChanged);
    }

    // Known keys map to a localized CallRes string; anything else (raw CallError text from the
    // hub, or a raw JS error message) is shown as-is — English only, a pre-existing limitation of
    // those specific server/browser messages, not something introduced here.
    private static string MapErrorMessage(string key) => key switch
    {
        nameof(StartCallStatus.AllUnavailable) => CallRes.AllUnavailable,
        nameof(StartCallStatus.CallerBusy) => CallRes.CallerBusy,
        nameof(StartCallStatus.NoAccess) => CallRes.NoAccess,
        "CallDeclined" => CallRes.CallDeclined,
        "CallUnavailable" => CallRes.CallUnavailable,
        "MediaPermissionDenied" => CallRes.MediaPermissionDenied,
        _ => key
    };

    private void DismissError() => _errorMessage = null;

    private void CloseGroupPicker() => _showGroupPicker = false;

    private async Task HandleGroupCallUsersPicked(List<ChatUserDto> users)
    {
        _showGroupPicker = false;
        await CallService.StartCallAsync(users.Select(u => u.Id).ToList());
    }

    private async Task HandleAccept() => await CallService.AcceptAsync();

    private async Task HandleDecline() => await CallService.DeclineAsync();

    public ValueTask DisposeAsync()
    {
        if (_callAccess)
        {
            CallService.StateChanged -= HandleStateChanged;
            CallService.ErrorOccurred -= HandleError;
            CallService.GroupCallPickerRequested -= HandleGroupCallPickerRequested;
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
