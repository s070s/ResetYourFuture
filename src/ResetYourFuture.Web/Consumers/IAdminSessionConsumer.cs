using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>Client consumer for admin scheduled-session management API operations.</summary>
public interface IAdminSessionConsumer
{
    Task<PagedResult<AdminScheduledSessionDto>?> GetAllAsync(int page = 1, int pageSize = 10, string sortBy = "startsatutc", string sortDir = "asc");
    Task<AdminScheduledSessionDto?> CreateAsync(SaveScheduledSessionRequest request);
    Task<AdminScheduledSessionDto?> UpdateAsync(Guid id, SaveScheduledSessionRequest request);
    Task<bool> CancelAsync(Guid id);
}
