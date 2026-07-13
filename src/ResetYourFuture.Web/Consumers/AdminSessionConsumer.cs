using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>HTTP consumer for the admin scheduled-session management API.</summary>
public class AdminSessionConsumer(HttpClient http, ResetYourFuture.Web.Services.ApiTokenProvider tokenProvider)
    : ApiClientBase(http, tokenProvider), IAdminSessionConsumer
{
    public Task<PagedResult<AdminScheduledSessionDto>?> GetAllAsync(int page = 1, int pageSize = 10, string sortBy = "startsatutc", string sortDir = "asc")
        => GetAsync<PagedResult<AdminScheduledSessionDto>>($"api/admin/sessions?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDir={sortDir}");

    public Task<AdminScheduledSessionDto?> CreateAsync(SaveScheduledSessionRequest request)
        => PostJsonAsync<SaveScheduledSessionRequest, AdminScheduledSessionDto>("api/admin/sessions", request);

    public Task<AdminScheduledSessionDto?> UpdateAsync(Guid id, SaveScheduledSessionRequest request)
        => PutJsonAsync<SaveScheduledSessionRequest, AdminScheduledSessionDto>($"api/admin/sessions/{id}", request);

    public Task<bool> CancelAsync(Guid id) => ActionAsync($"api/admin/sessions/{id}/cancel");
}
