using System.Linq.Expressions;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Entities;

namespace ResetYourFuture.Application.Mappings;

/// <summary>
/// Shared module/lesson admin mappers (MAINT-1). AdminModuleDto was hand-built at four sites
/// and AdminLessonDto at three inside their controllers. Keep each expression/extension pair's
/// field order in sync — they are the same projection for query and materialized use.
/// </summary>
public static class CourseContentMappings
{
    /// <summary>For IQueryable.Select (requires the Lessons navigation for the count).</summary>
    public static readonly Expression<Func<Module, AdminModuleDto>> ModuleAdminProjection =
        m => new AdminModuleDto(m.Id, m.TitleEn, m.TitleEl, m.DescriptionEn, m.DescriptionEl,
            m.SortOrder, m.CourseId, m.Lessons.Count);

    /// <summary>Materialized variant. A freshly created module's empty Lessons collection
    /// yields the same 0 the create endpoint used to pass explicitly.</summary>
    public static AdminModuleDto ToAdminDto(this Module m) =>
        new(m.Id, m.TitleEn, m.TitleEl, m.DescriptionEn, m.DescriptionEl,
            m.SortOrder, m.CourseId, m.Lessons.Count);

    /// <summary>For IQueryable.Select.</summary>
    public static readonly Expression<Func<Lesson, AdminLessonDto>> LessonAdminProjection =
        l => new AdminLessonDto(l.Id, l.TitleEn, l.TitleEl, l.ContentEn, l.ContentEl,
            l.PdfPath, l.VideoPath, l.DurationMinutes, l.SortOrder, l.ModuleId, l.IsPublished);

    public static AdminLessonDto ToAdminDto(this Lesson l) =>
        new(l.Id, l.TitleEn, l.TitleEl, l.ContentEn, l.ContentEl,
            l.PdfPath, l.VideoPath, l.DurationMinutes, l.SortOrder, l.ModuleId, l.IsPublished);
}
