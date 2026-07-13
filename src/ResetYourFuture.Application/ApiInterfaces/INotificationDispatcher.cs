using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Persists a notification (<see cref="INotificationService.CreateAsync"/>) then pushes it to
/// the recipient in real time if they're connected. Defined here (framework-agnostic) so
/// non-Web layers — <c>CertificateService</c> (Infrastructure), <c>SubscriptionService</c>
/// (Application) — can raise notifications without depending on SignalR; the Web-layer
/// implementation is the only place that touches <c>IHubContext</c>.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(
        string userId, NotificationType type, string titleKey, IReadOnlyList<string>? bodyArgs, string? linkUrl,
        CancellationToken cancellationToken = default);
}
