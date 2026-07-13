using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Layout;

public partial class NotificationBell : IAsyncDisposable
{
    private const int RecentCap = 10;

    [Inject] private INotificationConsumer Consumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ApiTokenProvider TokenProvider { get; set; } = default!;
    [Inject] private ILogger<NotificationBell> _logger { get; set; } = default!;

    private bool isOpen;
    private bool _accessChecked;
    private bool _loading = true;
    private int _unreadCount;
    private readonly List<NotificationDto> _recent = [];
    private HubConnection? _hub;

    private string BellAriaLabel => _unreadCount > 0
        ? string.Format(NotificationRes.UnreadBadgeAriaLabel, _unreadCount)
        : NotificationRes.OpenNotifications;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _accessChecked)
            return;

        _accessChecked = true;

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated != true)
            return;

        await LoadInitialAsync();
        await ConnectAsync();
    }

    private async Task LoadInitialAsync()
    {
        try
        {
            var summary = await Consumer.GetUnreadCountAsync();
            _unreadCount = summary?.UnreadCount ?? 0;

            var page = await Consumer.GetNotificationsAsync(1, RecentCap);
            if (page is not null)
                _recent.AddRange(page.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial notification state.");
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ConnectAsync()
    {
        var hubUrl = Navigation.ToAbsoluteUri("/hubs/notifications");

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => TokenProvider.GetTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<NotificationDto>("NotificationReceived", async notification =>
        {
            _recent.Insert(0, notification);
            if (_recent.Count > RecentCap)
                _recent.RemoveAt(_recent.Count - 1);
            if (!notification.IsRead)
                _unreadCount++;

            await InvokeAsync(StateHasChanged);
        });

        try
        {
            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start notification hub connection.");
        }
    }

    private void ToggleDropdown() => isOpen = !isOpen;

    private void Close() => isOpen = false;

    private async Task HandleFocusOut()
    {
        await Task.Delay(200);
        isOpen = false;
        StateHasChanged();
    }

    private async Task HandleClick(NotificationDto notification)
    {
        isOpen = false;

        if (!notification.IsRead)
        {
            var idx = _recent.FindIndex(n => n.Id == notification.Id);
            if (idx >= 0)
                _recent[idx] = _recent[idx] with { IsRead = true };
            _unreadCount = Math.Max(0, _unreadCount - 1);

            _ = Consumer.MarkReadAsync(notification.Id);
        }

        if (!string.IsNullOrEmpty(notification.LinkUrl))
            Navigation.NavigateTo(notification.LinkUrl);
    }

    private async Task MarkAllReadAsync()
    {
        if (await Consumer.MarkAllReadAsync())
        {
            for (var i = 0; i < _recent.Count; i++)
                _recent[i] = _recent[i] with { IsRead = true };
            _unreadCount = 0;
        }
    }

    private static string RelativeTime(DateTimeOffset createdAt)
    {
        var span = DateTimeOffset.UtcNow - createdAt;
        if (span < TimeSpan.FromMinutes(1)) return NotificationRes.TimeJustNow;
        if (span < TimeSpan.FromHours(1)) return string.Format(NotificationRes.TimeMinutesFormat, (int)span.TotalMinutes);
        if (span < TimeSpan.FromDays(1)) return string.Format(NotificationRes.TimeHoursFormat, (int)span.TotalHours);
        if (span < TimeSpan.FromDays(7)) return string.Format(NotificationRes.TimeDaysFormat, (int)span.TotalDays);
        return createdAt.ToLocalTime().ToString("MMM d");
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }
    }
}
