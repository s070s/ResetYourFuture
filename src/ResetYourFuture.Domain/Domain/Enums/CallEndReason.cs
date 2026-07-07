namespace ResetYourFuture.Domain.Enums;

/// <summary>
/// Why a call session ended. Stored as int.
/// </summary>
public enum CallEndReason
{
    Completed = 1,
    Missed = 2,
    Declined = 3,
    Cancelled = 4
}
