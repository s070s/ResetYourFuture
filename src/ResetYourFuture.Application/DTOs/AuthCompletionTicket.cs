namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Payload of the short-lived signed DataProtection token exchanged between
/// <c>AuthService.CreateSignInToken</c> (producer, Infrastructure) and the
/// <c>/auth/complete</c> minimal endpoint (consumer, Web). Serialized as JSON inside the
/// protector so producer and consumer share one compile-time-checked shape instead of an
/// order-sensitive delimited string.
/// </summary>
public record AuthCompletionTicket(
    string UserId,
    string? AdminBackupId,
    bool DeleteAdminBackup,
    string SecurityStamp,
    bool RememberMe);
