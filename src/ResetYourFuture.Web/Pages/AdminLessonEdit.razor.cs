using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Shared.Components.Forms;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminLessonEdit
{
    [Parameter]
    public Guid ModuleId
    {
        get; set;
    }

    [Inject] private IAdminLessonConsumer LessonConsumer { get; set; } = default!;
    [Inject] private IAdminModuleConsumer ModuleConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private List<AdminLessonDto>? lessons;
    private Guid? parentCourseId;
    private bool isSaving;
    private string message = string.Empty;
    private string messageType = "success";
    private Guid? _pendingDeleteLessonId;

    // Lesson modal fields
    private bool showLessonModal;
    private Guid? editingLessonId;
    private string lessonTitleEn = string.Empty;
    private string? lessonTitleEl;
    private string? lessonContentEn;
    private string? lessonContentEl;
    private int lessonSortOrder;
    private int? lessonDuration;
    private string? lessonPdfPath;
    private string? lessonVideoPath;
    private string? lessonVideoUrl;
    private QuillEditor? lessonContentEditorEn;
    private QuillEditor? lessonContentEditorEl;
    private IBrowserFile? pendingPdf;
    private IBrowserFile? pendingVideo;

    protected override async Task OnInitializedAsync()
    {
        await LoadLessons();
        await LoadParentCourseId();
    }

    private async Task LoadParentCourseId()
    {
        try
        {
            var module = await ModuleConsumer.GetModuleAsync(ModuleId);
            parentCourseId = module?.CourseId;
        }
        catch { /* parentCourseId remains null, back goes to courses list */ }
    }

    private async Task LoadLessons()
    {
        try
        {
            lessons = await LessonConsumer.GetLessonsByModuleAsync(ModuleId);
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private void ShowAddLesson()
    {
        editingLessonId = null;
        lessonTitleEn = string.Empty;
        lessonTitleEl = null;
        lessonContentEn = null;
        lessonContentEl = null;
        lessonVideoUrl = null;
        lessonSortOrder = (lessons?.Count ?? 0) + 1;
        lessonDuration = null;
        lessonPdfPath = null;
        lessonVideoPath = null;
        pendingPdf = null;
        pendingVideo = null;
        showLessonModal = true;
    }

    private void ShowEditLesson(AdminLessonDto lesson)
    {
        editingLessonId = lesson.Id;
        lessonTitleEn = lesson.TitleEn;
        lessonTitleEl = lesson.TitleEl;
        lessonContentEn = lesson.ContentEn;
        lessonContentEl = lesson.ContentEl;
        lessonVideoUrl = lesson.VideoPath;
        lessonSortOrder = lesson.SortOrder;
        lessonDuration = lesson.DurationMinutes;
        lessonPdfPath = lesson.PdfPath;
        lessonVideoPath = lesson.VideoPath;
        pendingPdf = null;
        pendingVideo = null;
        showLessonModal = true;
    }

    private void CloseLessonModal()
    {
        showLessonModal = false;
    }

    private void OnPdfSelected(InputFileChangeEventArgs e)
    {
        pendingPdf = e.File;
    }

    private void OnVideoSelected(InputFileChangeEventArgs e)
    {
        pendingVideo = e.File;
    }

    private async Task SaveLesson()
    {
        if (string.IsNullOrWhiteSpace(lessonTitleEn))
        {
            message = AdminRes.LessonTitleRequired;
            messageType = "danger";
            return;
        }

        isSaving = true;
        message = string.Empty;
        try
        {
            var contentEn = lessonContentEditorEn != null
                ? await lessonContentEditorEn.GetContentAsync()
                : lessonContentEn;

            var contentEl = lessonContentEditorEl != null
                ? await lessonContentEditorEl.GetContentAsync()
                : lessonContentEl;

            var request = new SaveLessonRequest(lessonTitleEn, lessonTitleEl, contentEn, contentEl, lessonVideoUrl, lessonDuration, lessonSortOrder, ModuleId);

            Guid lessonId;

            if (editingLessonId == null)
            {
                var created = await LessonConsumer.CreateLessonAsync(request);
                if (created is not null)
                {
                    lessonId = created.Id;
                }
                else
                {
                    message = AdminRes.LessonCreateFailed;
                    messageType = "danger";
                    return;
                }
            }
            else
            {
                lessonId = editingLessonId.Value;
                var updated = await LessonConsumer.UpdateLessonAsync(lessonId, request);
                if (updated is null)
                {
                    message = AdminRes.LessonUpdateFailed;
                    messageType = "danger";
                    return;
                }
            }

            // Upload files if selected; collect errors so they are visible to the admin.
            var uploadErrors = new System.Text.StringBuilder();
            if (pendingPdf != null)
            {
                var err = await UploadFileAsync(lessonId, pendingPdf, "pdf");
                if (err is not null) uploadErrors.AppendLine(err);
            }
            if (pendingVideo != null)
            {
                var err = await UploadFileAsync(lessonId, pendingVideo, "video");
                if (err is not null) uploadErrors.AppendLine(err);
            }

            await LoadLessons();
            CloseLessonModal();
            if (uploadErrors.Length > 0)
            {
                message = uploadErrors.ToString().Trim();
                messageType = "danger";
            }
            else
            {
                message = AdminRes.LessonSaved;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task<string?> UploadFileAsync(Guid lessonId, IBrowserFile file, string type)
    {
        var result = type == "pdf"
            ? await LessonConsumer.UploadPdfAsync(lessonId, file)
            : await LessonConsumer.UploadVideoAsync(lessonId, file);
        return result is not null ? null : $"Error uploading {type}";
    }

    private void DeleteLesson(Guid lessonId)
    {
        _pendingDeleteLessonId = lessonId;
    }

    private async Task ExecuteDeleteLessonAsync()
    {
        if (_pendingDeleteLessonId is not { } id)
            return;

        _pendingDeleteLessonId = null;

        try
        {
            var success = await LessonConsumer.DeleteLessonAsync(id);
            if (success)
            {
                await LoadLessons();
                message = AdminRes.LessonDeleted;
                messageType = "success";
            }
            else
            {
                message = AdminRes.LessonDeleteFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private void GoBack()
    {
        if (parentCourseId.HasValue)
        {
            Nav.NavigateTo($"/admin/courses/{parentCourseId.Value}");
        }
        else
        {
            Nav.NavigateTo("/admin/courses");
        }
    }
}
