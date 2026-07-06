namespace ResetYourFuture.Domain.Enums;

/// <summary>
/// Lifecycle state of a single participant within a call session.
/// Stored as int.
/// </summary>
public enum CallParticipantStatus
{
    Invited = 1,
    Joined = 2,
    Declined = 3,
    Missed = 4,
    Left = 5
}
