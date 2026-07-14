using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;

namespace ResetYourFuture.Web.Pages;

public partial class AdminUsers : IAsyncDisposable
{
    [Inject] private IAdminUserConsumer UserConsumer { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PagedResult<AdminUserDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string searchTerm = string.Empty;
    private string message = string.Empty;
    private string messageType = "success";
    private string _sortBy = "email";
    private string _sortDir = "asc";
    private string? confirmDeleteId;
    private CancellationTokenSource? _searchCts;
    private bool _loadFailed;

    private bool _resetPwdModalVisible;
    private string? _resetPwdUserId;
    private string _resetPwdEmail = string.Empty;
    private string _resetPwdNew = string.Empty;
    private string _resetPwdConfirm = string.Empty;
    private bool _resetPwdBusy;
    private string? _resetPwdError;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsers();
    }

    private async Task OnSort(string columnKey)
    {
        if (_sortBy == columnKey)
            _sortDir = _sortDir == "asc" ? "desc" : "asc";
        else
        {
            _sortBy = columnKey;
            _sortDir = "asc";
        }
        currentPage = 1;
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        _loadFailed = false;
        try
        {
            pagedResult = await UserConsumer.GetUsersAsync(
                currentPage,
                pageSize,
                string.IsNullOrEmpty(searchTerm) ? null : searchTerm,
                _sortBy,
                _sortDir);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            message = ErrorMessagesRes.AccessDenied;
            messageType = "danger";
            _loadFailed = true;
        }
        catch (Exception ex)
        {
            // UX-6: any other failure (network, 500, etc.) used to be unhandled here and crash
            // the circuit; pagedResult stays null so the skeleton would otherwise spin forever.
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
            _loadFailed = true;
        }
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadUsers();
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        currentPage = 1;

        var previous = _searchCts;
        _searchCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(300, _searchCts.Token);
            await LoadUsers();
        }
        catch (OperationCanceledException) { }
    }

    private async Task GoToPage(int page)
    {
        currentPage = page;
        await LoadUsers();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadUsers();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadUsers();
        }
    }

    private async Task ImpersonateUser(string userId)
    {
        var result = await AuthService.ImpersonateAsync(userId);
        if (result.Success)
        {
            Navigation.NavigateTo(
                $"/auth/complete?ticket={Uri.EscapeDataString(result.Token!)}&returnUrl=%2F",
                forceLoad: true);
        }
        else
        {
            message = result.Message ?? AdminRes.ImpersonationFailed;
            messageType = "danger";
        }
    }

    private void OpenResetPasswordModal(AdminUserDto user)
    {
        _resetPwdUserId = user.Id;
        _resetPwdEmail = user.Email;
        _resetPwdNew = string.Empty;
        _resetPwdConfirm = string.Empty;
        _resetPwdError = null;
        _resetPwdModalVisible = true;
    }

    private void CloseResetPasswordModal()
    {
        _resetPwdModalVisible = false;
        _resetPwdUserId = null;
        _resetPwdNew = string.Empty;
        _resetPwdConfirm = string.Empty;
        _resetPwdError = null;
    }

    private async Task SubmitResetPassword()
    {
        if (string.IsNullOrWhiteSpace(_resetPwdNew) || _resetPwdNew.Length < 8)
        {
            _resetPwdError = AdminRes.PasswordMinLength;
            return;
        }

        if (_resetPwdNew != _resetPwdConfirm)
        {
            _resetPwdError = AdminRes.PasswordMismatch;
            return;
        }

        _resetPwdBusy = true;
        _resetPwdError = null;

        try
        {
            var success = await UserConsumer.SetPasswordAsync(_resetPwdUserId!, _resetPwdNew);
            if (success)
            {
                message = AdminRes.PasswordUpdated;
                messageType = "success";
                CloseResetPasswordModal();
            }
            else
            {
                _resetPwdError = AdminRes.PasswordUpdateFailed;
            }
        }
        catch (Exception ex)
        {
            _resetPwdError = ErrorMessagesRes.UnexpectedErrorTryAgain;
        }
        finally
        {
            _resetPwdBusy = false;
        }
    }

    private async Task SetUserEnabled(string userId, bool enable)
    {
        try
        {
            // API-8: the row already knows the target state (that's how it chose which
            // button to show), so this calls the idempotent enable/disable endpoint
            // directly instead of a toggle that two concurrent admins could race.
            var success = enable
                ? await UserConsumer.EnableUserAsync(userId)
                : await UserConsumer.DisableUserAsync(userId);
            if (success)
            {
                await LoadUsers();
                message = AdminRes.UserStatusToggled;
                messageType = "success";
            }
            else
            {
                message = AdminRes.UserToggleFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private void RequestDeleteUser(string userId) => confirmDeleteId = userId;

    private void CancelDeleteUser() => confirmDeleteId = null;

    private async Task DeleteUser()
    {
        if (confirmDeleteId is not { } userId)
            return;

        confirmDeleteId = null;

        try
        {
            var success = await UserConsumer.DeleteUserAsync(userId);
            if (success)
            {
                await LoadUsers();
                message = AdminRes.UserDeleted;
                messageType = "success";
            }
            else
            {
                message = AdminRes.UserDeleteFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}

