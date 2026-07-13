using Microsoft.AspNetCore.Components;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.Resources;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Sessions
{
    [Inject] private ISessionConsumer SessionConsumer { get; set; } = default!;
    [Inject] private ICallService CallService { get; set; } = default!;

    private List<ScheduledSessionListItemDto> _sessions = [];
    private bool _loading = true;
    private bool _joining;
    private string? _error;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _sessions = [.. await SessionConsumer.GetUpcomingAsync(CurrentLang)];
        _loading = false;
    }

    private async Task RegisterAsync(Guid id)
    {
        _error = null;
        if (await SessionConsumer.RegisterAsync(id))
            await LoadAsync();
        else
            _error = SessionRes.RegisterError;
    }

    private async Task UnregisterAsync(Guid id)
    {
        _error = null;
        if (await SessionConsumer.UnregisterAsync(id))
            await LoadAsync();
    }

    /// <summary>
    /// The first host/registrant to click this rings everyone else via the existing group-call
    /// flow (ICallService.StartCallAsync) — no server-materialized call, full reuse of the
    /// already-proven ring/accept/media stack. The resulting CallSessionId is then persisted so
    /// later visitors see "call in progress" instead of starting a second, separate call.
    /// </summary>
    private async Task JoinOrStartAsync(ScheduledSessionListItemDto session)
    {
        if (session.OtherParticipantUserIds.Count == 0)
            return;

        _error = null;
        _joining = true;
        try
        {
            await CallService.EnsureConnectedAsync();
            await CallService.StartCallAsync([.. session.OtherParticipantUserIds]);

            if (CallService.ActiveCallId is { } callId)
            {
                await SessionConsumer.LinkCallAsync(session.Id, callId);
                await LoadAsync();
            }
            else
            {
                _error = SessionRes.JoinError;
            }
        }
        finally
        {
            _joining = false;
        }
    }
}
