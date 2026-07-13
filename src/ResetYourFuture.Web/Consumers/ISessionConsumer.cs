using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>Client consumer for the upcoming-sessions API.</summary>
public interface ISessionConsumer
{
    Task<IReadOnlyList<ScheduledSessionListItemDto>> GetUpcomingAsync(string lang = "en");
    Task<bool> RegisterAsync(Guid id);
    Task<bool> UnregisterAsync(Guid id);
    Task<bool> LinkCallAsync(Guid id, Guid callSessionId);
}
