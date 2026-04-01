using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Shared;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Shared.Components.Forms;

public partial class LessonEditorModal
{
    [Parameter, EditorRequired] public bool IsVisible { get; set; }
    [Parameter] public AdminLessonDto? EditingLesson { get; set; }
    [Parameter, EditorRequired] public Guid ModuleId { get; set; }
    [Parameter] public int DefaultSortOrder { get; set; } = 1;
    [Parameter, EditorRequired] public EventCallback OnClose { get; set; }
    [Parameter, EditorRequired] public EventCallback OnSaved { get; set; }

    [Inject] private IAdminLessonConsumer LessonConsumer { get; set; } = default!;

    private string _titleEn = string.Empty;
    private string? _titleEl;
    private string? _contentEn;
    private string? _contentEl;
    private string? _videoUrl;
    private int _sortOrder;
    private int? _duration;
    private string? _existingPdfPath;
    private string? _existingVideoPath;
    private IBrowserFile? _pendingPdf;
    private IBrowserFile? _pendingVideo;
    private QuillEditor? _contentEditorEn;
    private QuillEditor? _contentEditorEl;
    private bool _isSaving;
    private string _errorMessage = string.Empty;
    private bool _wasVisible;

    protected override void OnParametersSet()
    {
        var justOpened = IsVisible && !_wasVisible;
        _wasVisible = IsVisible;

        if ( !justOpened )
            return;

        _errorMessage = string.Empty;
        _pendingPdf = null;
        _pendingVideo = null;

        if ( EditingLesson is not null )
        {
            _titleEn = EditingLesson.TitleEn;
            _titleEl = EditingLesson.TitleEl;
            _contentEn = EditingLesson.ContentEn;
            _contentEl = EditingLesson.ContentEl;
            _videoUrl = EditingLesson.VideoPath;
            _sortOrder = EditingLesson.SortOrder;
            _duration = EditingLesson.DurationMinutes;
            _existingPdfPath = EditingLesson.PdfPath;
            _existingVideoPath = EditingLesson.VideoPath;
        }
        else
        {
            _titleEn = string.Empty;
            _titleEl = null;
            _contentEn = null;
            _contentEl = null;
            _videoUrl = null;
            _sortOrder = DefaultSortOrder;
            _duration = null;
            _existingPdfPath = null;
            _existingVideoPath = null;
        }
    }

    private void OnPdfSelected( InputFileChangeEventArgs e ) => _pendingPdf = e.File;
    private void OnVideoSelected( InputFileChangeEventArgs e ) => _pendingVideo = e.File;

    private async Task SaveAsync()
    {
        if ( string.IsNullOrWhiteSpace( _titleEn ) )
        {
            _errorMessage = "Lesson title is required.";
            return;
        }

        _errorMessage = string.Empty;
        _isSaving = true;
        try
        {
            var contentEn = _contentEditorEn != null
                ? await _contentEditorEn.GetContentAsync()
                : _contentEn;

            var contentEl = _contentEditorEl != null
                ? await _contentEditorEl.GetContentAsync()
                : _contentEl;

            var request = new SaveLessonRequest(
                _titleEn , _titleEl , contentEn , contentEl , _videoUrl , _duration , _sortOrder , ModuleId );

            Guid lessonId;

            if ( EditingLesson is null )
            {
                var created = await LessonConsumer.CreateLessonAsync( request );
                if ( created is null )
                {
                    _errorMessage = "Error creating lesson.";
                    return;
                }
                lessonId = created.Id;
            }
            else
            {
                lessonId = EditingLesson.Id;
                var updated = await LessonConsumer.UpdateLessonAsync( lessonId , request );
                if ( updated is null )
                {
                    _errorMessage = "Error updating lesson.";
                    return;
                }
            }

            var uploadErrors = new System.Text.StringBuilder();

            if ( _pendingPdf != null )
            {
                var result = await LessonConsumer.UploadPdfAsync( lessonId , _pendingPdf );
                if ( result is null ) uploadErrors.AppendLine( "Error uploading pdf." );
            }

            if ( _pendingVideo != null )
            {
                var result = await LessonConsumer.UploadVideoAsync( lessonId , _pendingVideo );
                if ( result is null ) uploadErrors.AppendLine( "Error uploading video." );
            }

            if ( uploadErrors.Length > 0 )
            {
                _errorMessage = uploadErrors.ToString().Trim();
                return;
            }

            await OnSaved.InvokeAsync();
        }
        catch ( Exception ex )
        {
            _errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }
}
