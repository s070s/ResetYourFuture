using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Shared;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminLessonEdit
{
    [Parameter]
    public Guid ModuleId
    {
        get; set;
    }

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private List<AdminLessonDto>? lessons;
    private Guid? parentCourseId;
    private bool isSaving;
    private string message = string.Empty;

    // Lesson modal fields
    private bool showLessonModal;
    private Guid? editingLessonId;
    private string lessonTitle = string.Empty;
    private string? lessonContent;
    private int lessonSortOrder;
    private int? lessonDuration;
    private string? lessonPdfPath;
    private string? lessonVideoPath;
    private string? lessonVideoUrl;
    private QuillEditor? lessonContentEditor;
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
            var module = await Http.GetFromJsonAsync<AdminModuleDto>( $"api/admin/modules/{ModuleId}" );
            parentCourseId = module?.CourseId;
        }
        catch { /* parentCourseId remains null, back goes to courses list */ }
    }

    private async Task LoadLessons()
    {
        try
        {
            lessons = await Http.GetFromJsonAsync<List<AdminLessonDto>>( $"api/admin/lessons/module/{ModuleId}" );
        }
        catch ( Exception ex )
        {
            message = $"Error loading lessons: {ex.Message}";
        }
    }

    private void ShowAddLesson()
    {
        editingLessonId = null;
        lessonTitle = string.Empty;
        lessonContent = null;
        lessonVideoUrl = null;
        lessonSortOrder = ( lessons?.Count ?? 0 ) + 1;
        lessonDuration = null;
        lessonPdfPath = null;
        lessonVideoPath = null;
        pendingPdf = null;
        pendingVideo = null;
        showLessonModal = true;
    }

    private void ShowEditLesson( AdminLessonDto lesson )
    {
        editingLessonId = lesson.Id;
        lessonTitle = lesson.Title;
        lessonContent = lesson.Content;
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

    private void OnPdfSelected( InputFileChangeEventArgs e )
    {
        pendingPdf = e.File;
    }

    private void OnVideoSelected( InputFileChangeEventArgs e )
    {
        pendingVideo = e.File;
    }

    private async Task SaveLesson()
    {
        if ( string.IsNullOrWhiteSpace( lessonTitle ) )
        {
            message = "Lesson title is required.";
            return;
        }

        isSaving = true;
        message = string.Empty;
        try
        {
            var content = lessonContentEditor != null
                ? await lessonContentEditor.GetContentAsync()
                : lessonContent;

            var request = new SaveLessonRequest( lessonTitle , content , lessonVideoUrl , lessonDuration , lessonSortOrder , ModuleId );

            HttpResponseMessage response;
            Guid lessonId;

            if ( editingLessonId == null )
            {
                response = await Http.PostAsJsonAsync( "api/admin/lessons" , request );
                if ( response.IsSuccessStatusCode )
                {
                    var created = await response.Content.ReadFromJsonAsync<AdminLessonDto>();
                    lessonId = created!.Id;
                }
                else
                {
                    message = $"Error creating lesson: {response.ReasonPhrase}";
                    return;
                }
            }
            else
            {
                lessonId = editingLessonId.Value;
                response = await Http.PutAsJsonAsync( $"api/admin/lessons/{lessonId}" , request );
                if ( !response.IsSuccessStatusCode )
                {
                    message = $"Error updating lesson: {response.ReasonPhrase}";
                    return;
                }
            }

            // Upload files if selected; collect errors so they are visible to the admin.
            var uploadErrors = new System.Text.StringBuilder();
            if ( pendingPdf != null )
            {
                var err = await UploadFileAsync( lessonId , pendingPdf , "pdf" );
                if ( err is not null ) uploadErrors.AppendLine( err );
            }
            if ( pendingVideo != null )
            {
                var err = await UploadFileAsync( lessonId , pendingVideo , "video" );
                if ( err is not null ) uploadErrors.AppendLine( err );
            }

            await LoadLessons();
            CloseLessonModal();
            message = uploadErrors.Length > 0 ? uploadErrors.ToString().Trim() : "Lesson saved";
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task<string?> UploadFileAsync( Guid lessonId , IBrowserFile file , string type )
    {
        const long maxFileSize = 500L * 1024 * 1024; // 500 MB — matches server limit
        using var formContent = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        if ( !string.IsNullOrEmpty( file.ContentType ) )
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        formContent.Add( streamContent , "file" , file.Name );
        var uploadResponse = await Http.PostAsync( $"api/admin/lessons/{lessonId}/upload/{type}" , formContent );
        return uploadResponse.IsSuccessStatusCode
            ? null
            : $"Error uploading {type}: {uploadResponse.StatusCode} {uploadResponse.ReasonPhrase}";
    }

    private async Task DeleteLesson( Guid lessonId )
    {
        if ( !await JSRuntime.InvokeAsync<bool>( "confirm" , "Delete this lesson?" ) )
            return;

        try
        {
            var response = await Http.DeleteAsync( $"api/admin/lessons/{lessonId}" );
            if ( response.IsSuccessStatusCode )
            {
                await LoadLessons();
                message = "Lesson deleted";
            }
            else
            {
                message = "Error deleting lesson";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private void GoBack()
    {
        if ( parentCourseId.HasValue )
        {
            Nav.NavigateTo( $"/admin/courses/{parentCourseId.Value}" );
        }
        else
        {
            Nav.NavigateTo( "/admin/courses" );
        }
    }
}
