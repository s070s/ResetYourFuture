using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Shared;
using ResetYourFuture.Shared.Models.Admin;
using System.Net.Http.Json;
using System.Text.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminCourseEdit
{
    [Parameter]
    public Guid CourseId
    {
        get; set;
    }

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool IsNew => CourseId == Guid.Empty;
    private AdminCourseDto? course;
    private List<AdminModuleDto>? modules;
    private Dictionary<Guid , List<AdminLessonDto>> lessonsMap = new();
    private HashSet<Guid> expandedModules = new();
    private bool isSaving;
    private string message = string.Empty;

    // Course fields
    private string courseTitle = string.Empty;
    private string? courseDescription;
    private QuillEditor? descriptionEditor;

    // Module modal fields
    private bool showModuleModal;
    private Guid? editingModuleId;
    private string moduleTitle = string.Empty;
    private string? moduleDescription;
    private int moduleSortOrder;

    // Lesson modal fields
    private bool showLessonModal;
    private Guid? editingLessonId;
    private Guid lessonModuleId;
    private string lessonTitle = string.Empty;
    private string? lessonContent;
    private string? lessonVideoUrl;
    private int lessonSortOrder;
    private int? lessonDuration;
    private string? lessonPdfPath;
    private string? lessonVideoFilePath;
    private QuillEditor? lessonContentEditor;
    private IBrowserFile? pendingPdf;
    private IBrowserFile? pendingVideo;
    private bool isLessonSaving;
    private Guid _lastLoadedCourseId;

    protected override async Task OnParametersSetAsync()
    {
        // Guard: only reload when CourseId actually changes (handles same-component navigation)
        if ( CourseId == _lastLoadedCourseId )
            return;

        _lastLoadedCourseId = CourseId;

        // Reset state for the new course
        course = null;
        modules = null;
        lessonsMap = new();
        expandedModules = new();
        message = string.Empty;

        if ( !IsNew )
        {
            await LoadCourse();
            await LoadModulesAndLessons();
        }
    }

    // ── Data loading ──

    private async Task LoadCourse()
    {
        try
        {
            course = await Http.GetFromJsonAsync<AdminCourseDto>( $"api/admin/courses/{CourseId}" );
            if ( course != null )
            {
                courseTitle = course.Title;
                courseDescription = course.Description;
            }
        }
        catch ( Exception ex )
        {
            message = $"Error loading course: {ex.Message}";
        }
    }

    private async Task LoadModulesAndLessons()
    {
        try
        {
            modules = await Http.GetFromJsonAsync<List<AdminModuleDto>>( $"api/admin/modules/course/{CourseId}" );
            if ( modules != null )
            {
                foreach ( var m in modules )
                {
                    await LoadLessonsForModule( m.Id );
                }
            }
        }
        catch ( Exception ex )
        {
            message = $"Error loading modules: {ex.Message}";
        }
    }

    private async Task LoadLessonsForModule( Guid moduleId )
    {
        try
        {
            var lessons = await Http.GetFromJsonAsync<List<AdminLessonDto>>(
                $"api/admin/lessons/module/{moduleId}" ) ?? [];
            lessonsMap [ moduleId ] = lessons;
        }
        catch
        {
            lessonsMap [ moduleId ] = [];
        }
    }

    private void ToggleModule( Guid moduleId )
    {
        if ( !expandedModules.Remove( moduleId ) )
        {
            expandedModules.Add( moduleId );
        }
    }

    // ── Course save ──

    private async Task SaveCourse()
    {
        isSaving = true;
        message = string.Empty;
        try
        {
            var desc = descriptionEditor != null
                ? await descriptionEditor.GetContentAsync()
                : courseDescription;

            var request = new SaveCourseRequest( courseTitle , desc );

            HttpResponseMessage response;
            if ( IsNew )
            {
                response = await Http.PostAsJsonAsync( "api/admin/courses" , request );
            }
            else
            {
                response = await Http.PutAsJsonAsync( $"api/admin/courses/{CourseId}" , request );
            }

            if ( response.IsSuccessStatusCode )
            {
                if ( IsNew )
                {
                    var created = await response.Content.ReadFromJsonAsync<AdminCourseDto>();
                    if ( created != null )
                    {
                        Nav.NavigateTo( $"/admin/courses/{created.Id}" );
                        return;
                    }
                }
                await LoadCourse();
                message = "Course saved successfully";
            }
            else
            {
                message = $"Error saving course: {response.ReasonPhrase}";
            }
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

    private void Cancel()
    {
        Nav.NavigateTo( "/admin/courses" );
    }

    // ── Module modal ──

    private void ShowAddModule()
    {
        editingModuleId = null;
        moduleTitle = string.Empty;
        moduleDescription = null;
        moduleSortOrder = ( modules?.Count ?? 0 ) + 1;
        showModuleModal = true;
    }

    private void ShowEditModule( AdminModuleDto module )
    {
        editingModuleId = module.Id;
        moduleTitle = module.Title;
        moduleDescription = module.Description;
        moduleSortOrder = module.SortOrder;
        showModuleModal = true;
    }

    private void CloseModuleModal()
    {
        showModuleModal = false;
    }

    private async Task SaveModule()
    {
        try
        {
            var request = new SaveModuleRequest( moduleTitle , moduleDescription , moduleSortOrder , CourseId );

            HttpResponseMessage response;
            if ( editingModuleId == null )
            {
                response = await Http.PostAsJsonAsync( "api/admin/modules" , request );
            }
            else
            {
                response = await Http.PutAsJsonAsync( $"api/admin/modules/{editingModuleId}" , request );
            }

            if ( response.IsSuccessStatusCode )
            {
                await LoadModulesAndLessons();
                CloseModuleModal();
                message = "Module saved";
            }
            else
            {
                message = $"Error saving module: {response.ReasonPhrase}";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteModule( Guid moduleId )
    {
        if ( !await JSRuntime.InvokeAsync<bool>( "confirm" , "Delete this module and all its lessons?" ) )
            return;

        try
        {
            var response = await Http.DeleteAsync( $"api/admin/modules/{moduleId}" );
            if ( response.IsSuccessStatusCode )
            {
                lessonsMap.Remove( moduleId );
                await LoadModulesAndLessons();
                message = "Module deleted";
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                message = $"Error: {body}";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    // ── Lesson modal ──

    private void ShowAddLesson( Guid moduleId )
    {
        editingLessonId = null;
        lessonModuleId = moduleId;
        lessonTitle = string.Empty;
        lessonContent = null;
        lessonVideoUrl = null;
        lessonSortOrder = ( lessonsMap.GetValueOrDefault( moduleId )?.Count ?? 0 ) + 1;
        lessonDuration = null;
        lessonPdfPath = null;
        lessonVideoFilePath = null;
        pendingPdf = null;
        pendingVideo = null;
        showLessonModal = true;
    }

    private void ShowEditLesson( AdminLessonDto lesson )
    {
        editingLessonId = lesson.Id;
        lessonModuleId = lesson.ModuleId;
        lessonTitle = lesson.Title;
        lessonContent = lesson.Content;
        lessonVideoUrl = lesson.VideoPath;
        lessonSortOrder = lesson.SortOrder;
        lessonDuration = lesson.DurationMinutes;
        lessonPdfPath = lesson.PdfPath;
        lessonVideoFilePath = lesson.VideoPath;
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
        isLessonSaving = true;
        message = string.Empty;
        try
        {
            var content = lessonContentEditor != null
                ? await lessonContentEditor.GetContentAsync()
                : lessonContent;

            var request = new SaveLessonRequest(
                lessonTitle , content , lessonVideoUrl , lessonDuration , lessonSortOrder , lessonModuleId );

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

            // Upload files if selected
            if ( pendingPdf != null )
            {
                await UploadFileAsync( lessonId , pendingPdf , "pdf" );
            }
            if ( pendingVideo != null )
            {
                await UploadFileAsync( lessonId , pendingVideo , "video" );
            }

            await LoadLessonsForModule( lessonModuleId );
            // Refresh module counts
            await LoadModulesAndLessons();
            CloseLessonModal();
            message = "Lesson saved";
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
        finally
        {
            isLessonSaving = false;
        }
    }

    private async Task UploadFileAsync( Guid lessonId , IBrowserFile file , string type )
    {
        const long maxFileSize = 200 * 1024 * 1024; // 200 MB
        using var formContent = new MultipartFormDataContent();
        using var stream = file.OpenReadStream( maxFileSize );
        var streamContent = new StreamContent( stream );
        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
        formContent.Add( streamContent , "file" , file.Name );
        await Http.PostAsync( $"api/admin/lessons/{lessonId}/upload/{type}" , formContent );
    }

    private async Task DeleteLesson( Guid lessonId , Guid moduleId )
    {
        if ( !await JSRuntime.InvokeAsync<bool>( "confirm" , "Delete this lesson?" ) )
            return;

        try
        {
            var response = await Http.DeleteAsync( $"api/admin/lessons/{lessonId}" );
            if ( response.IsSuccessStatusCode )
            {
                await LoadLessonsForModule( moduleId );
                await LoadModulesAndLessons();
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

    // ── Publish ──

    private async Task PublishCourse()
    {
        try
        {
            var response = await Http.PostAsync( $"api/admin/courses/{CourseId}/publish" , null );
            if ( response.IsSuccessStatusCode )
            {
                await LoadCourse();
                message = "Course published";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task UnpublishCourse()
    {
        try
        {
            var response = await Http.PostAsync( $"api/admin/courses/{CourseId}/unpublish" , null );
            if ( response.IsSuccessStatusCode )
            {
                await LoadCourse();
                message = "Course unpublished";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    // ── Course Structure Preview (mirrors the seed JSON format) ──

    private string GenerateCoursePreviewJson()
    {
        if ( modules == null )
            return "{}";

        var preview = new
        {
            title = courseTitle ,
            description = courseDescription ,
            isPublished = course?.IsPublished ?? false ,
            modules = modules.OrderBy( m => m.SortOrder ).Select( m =>
            {
                var moduleLessons = lessonsMap.GetValueOrDefault( m.Id ) ?? [];
                return new
                {
                    title = m.Title ,
                    description = m.Description ,
                    sortOrder = m.SortOrder ,
                    lessons = moduleLessons.OrderBy( l => l.SortOrder ).Select( l =>
                    {
                        var dict = new Dictionary<string , object?>
                        {
                            [ "title" ] = l.Title ,
                            [ "sortOrder" ] = l.SortOrder
                        };

                        if ( !string.IsNullOrEmpty( l.VideoPath ) )
                            dict [ "videoPath" ] = l.VideoPath;

                        if ( !string.IsNullOrEmpty( l.Content ) )
                        {
                            // Show a truncated preview for readability
                            var preview = l.Content.Length > 80
                                ? l.Content [ ..80 ] + "..."
                                : l.Content;
                            dict [ "content" ] = preview;
                        }

                        if ( !string.IsNullOrEmpty( l.PdfPath ) )
                            dict [ "pdfPath" ] = l.PdfPath;

                        if ( l.DurationMinutes.HasValue )
                            dict [ "durationMinutes" ] = l.DurationMinutes;

                        return dict;
                    } ).ToList()
                };
            } ).ToList()
        };

        return JsonSerializer.Serialize( preview , new JsonSerializerOptions { WriteIndented = true } );
    }
}
