namespace ResetYourFuture.Web.Domain.Enums;

/// <summary>
/// Represents enrollment completion state.
/// Stored as int; extensible for partial/paused states later.
/// </summary>
public enum EnrollmentStatus
{
    Active = 1,
    Completed = 2,
    Dropped = 3
}
