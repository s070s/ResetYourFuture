using System.Linq.Expressions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Application.Mappings;

/// <summary>
/// Shared scheduled-session admin mapper (MAINT-1). The 12-field projection (including the
/// DisplayName-or-full-name host fallback) was duplicated between the admin list query and
/// the single-fetch used after create/update in ScheduledSessionService.
/// </summary>
public static class SessionMappings
{
    /// <summary>For IQueryable.Select (requires Host/Course/Registrations navigations).</summary>
    public static readonly Expression<Func<ScheduledSession, AdminScheduledSessionDto>> AdminProjection =
        s => new AdminScheduledSessionDto(
            s.Id, s.TitleEn, s.TitleEl,
            !string.IsNullOrWhiteSpace(s.Host!.DisplayName) ? s.Host.DisplayName! : (s.Host.FirstName + " " + s.Host.LastName).Trim(),
            s.CourseId, s.Course != null ? s.Course.TitleEn : null,
            s.StartsAtUtc, s.DurationMinutes, s.MaxParticipants, s.Registrations.Count,
            s.Status.ToString(), s.CreatedAt);
}
