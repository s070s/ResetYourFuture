using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>HTTP consumer for the upcoming-sessions API.</summary>
public class SessionConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider) : ApiClientBase(http, tokenProvider), ISessionConsumer
{
    public async Task<IReadOnlyList<ScheduledSessionListItemDto>> GetUpcomingAsync(string lang = "en")
        => await GetAsync<List<ScheduledSessionListItemDto>>($"api/sessions?lang={lang}") ?? [];

    public Task<bool> RegisterAsync(Guid id) => ActionAsync($"api/sessions/{id}/register");

    public Task<bool> UnregisterAsync(Guid id) => ActionAsync($"api/sessions/{id}/unregister");

    public Task<bool> LinkCallAsync(Guid id, Guid callSessionId)
        => PostJsonActionAsync($"api/sessions/{id}/link-call", new LinkCallSessionRequest(callSessionId));
}
