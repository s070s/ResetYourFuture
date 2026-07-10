using ResetYourFuture.Shared.Resources;

namespace ResetYourFuture.Web.Extensions;

/// <summary>
/// Localized "last seen X ago" strings for presence indicators. Buckets coarsen with age
/// (minutes → hours → days) and fall back to a plain local date beyond a week, where a
/// relative phrase stops being meaningful.
/// </summary>
public static class RelativeTimeFormatter
{
    public static string LastSeen(DateTime lastSeenUtc)
    {
        var delta = DateTime.UtcNow - lastSeenUtc;
        return delta switch
        {
            { TotalMinutes: < 1 } => GlobalRes.LastSeenJustNow,
            { TotalHours: < 1 } => string.Format(GlobalRes.LastSeenMinutesFormat, (int)delta.TotalMinutes),
            { TotalDays: < 1 } => string.Format(GlobalRes.LastSeenHoursFormat, (int)delta.TotalHours),
            { TotalDays: <= 7 } => string.Format(GlobalRes.LastSeenDaysFormat, (int)delta.TotalDays),
            _ => string.Format(GlobalRes.LastSeenDateFormat, lastSeenUtc.ToLocalTime().ToString("d"))
        };
    }
}
