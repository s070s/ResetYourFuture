namespace ResetYourFuture.Web.Identity;

/// <summary>
/// Extensible user status. Add values as needed; avoid removing existing ones.
/// Stored as int in DB for forward compatibility.
/// </summary>
public enum UserStatus
{
    Unknown = 0,
    Student = 1,
    Graduate = 2,
    NEET = 3,       // Not in Education, Employment, or Training
    Other = 99
}
