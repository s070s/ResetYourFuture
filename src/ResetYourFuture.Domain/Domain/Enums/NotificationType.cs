namespace ResetYourFuture.Domain.Enums;

/// <summary>
/// What kind of event a <see cref="Entities.Notification"/> represents. Stored as int.
/// Drives which resx TitleKey the row was created with and is available to clients for
/// per-type icons/filtering.
/// </summary>
public enum NotificationType
{
    ChatMessage = 1,
    CertificateIssued = 2,
    SubscriptionActivated = 3,
    SubscriptionExpiring = 4,
    ReviewApproved = 5,
    SessionReminder = 6
}
