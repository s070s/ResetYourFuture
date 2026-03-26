using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Client.Shared;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminCourseEdit
{
    [Parameter]
    public Guid CourseId
    {
        get; set;
    }

    [Inject] private IAdminCourseConsumer CourseConsumer { get; set; } = default!;
    [Inject] private IAdminModuleConsumer ModuleConsumer { get; set; } = default!;
    [Inject] private IAdminLessonConsumer LessonConsumer { get; set; } = default!;
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
    private string courseTitleEn = string.Empty;
    private string? courseTitleEl;
    private string? courseDescriptionEn;
    private string? courseDescriptionEl;
    private QuillEditor? descriptionEditorEn;
    private QuillEditor? descriptionEditorEl;

    // Module modal fields
    private bool showModuleModal;
    private Guid? editingModuleId;
    private string moduleTitleEn = string.Empty;
    private string? moduleTitleEl;
    private string? moduleDescriptionEn;
    private string? moduleDescriptionEl;
    private int moduleSortOrder;

    // Lesson modal fields
    private bool showLessonModal;
    private Guid? editingLessonId;
    private Guid lessonModuleId;
    private string lessonTitleEn = string.Empty;
    private string? lessonTitleEl;
    private string? lessonContentEn;
    private string? lessonContentEl;
    private string? lessonVideoUrl;
    private int lessonSortOrder;
    private int? lessonDuration;
    private string? lessonPdfPath;
    private string? lessonVideoFilePath;
    private QuillEditor? lessonContentEditorEn;
    private QuillEditor? lessonContentEditorEl;
    private IBrowserFile? pendingPdf;
    private IBrowserFile? pendingVideo;
    private bool isLessonSaving;
    private Guid _lastLoadedCourseId;
    private string _coursePreviewJson = "{}";
    private static readonly JsonSerializerOptions _previewJsonOptions = new() { WriteIndented = true };

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
            course = await CourseConsumer.GetCourseAsync( CourseId );
            if ( course != null )
            {
                courseTitleEn = course.TitleEn;
                courseTitleEl = course.TitleEl;
                courseDescriptionEn = course.DescriptionEn;
                courseDescriptionEl = course.DescriptionEl;
            }
            UpdatePreviewJson();
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
            modules = await ModuleConsumer.GetModulesByCourseAsync( CourseId );
            lessonsMap = new();
            if ( modules != null )
            {
                modules = [.. modules.OrderBy( m => m.SortOrder )];
                await Task.WhenAll( modules.Select( m => LoadLessonsForModule( m.Id ) ) );
            }
            UpdatePreviewJson();
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
            var lessons = await LessonConsumer.GetLessonsByModuleAsync( moduleId );
            lessonsMap [ moduleId ] = [.. lessons.OrderBy( l => l.SortOrder )];
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
            var descEn = descriptionEditorEn != null
                ? await descriptionEditorEn.GetContentAsync()
                : courseDescriptionEn;

            var descEl = descriptionEditorEl != null
                ? await descriptionEditorEl.GetContentAsync()
                : courseDescriptionEl;

            var request = new SaveCourseRequest( courseTitleEn , courseTitleEl , descEn , descEl );

            if ( IsNew )
            {
                var created = await CourseConsumer.CreateCourseAsync( request );
                if ( created is not null )
                {
                    Nav.NavigateTo( $"/admin/courses/{created.Id}" );
                    return;
                }
                message = "Error saving course";
            }
            else
            {
                var updated = await CourseConsumer.UpdateCourseAsync( CourseId , request );
                if ( updated is not null )
                {
                    await LoadCourse();
                    message = "Course saved successfully";
                }
                else
                {
                    message = "Error saving course";
                }
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
        moduleTitleEn = string.Empty;
        moduleTitleEl = null;
        moduleDescriptionEn = null;
        moduleDescriptionEl = null;
        moduleSortOrder = ( modules?.Count ?? 0 ) + 1;
        showModuleModal = true;
    }

    private void ShowEditModule( AdminModuleDto module )
    {
        editingModuleId = module.Id;
        moduleTitleEn = module.TitleEn;
        moduleTitleEl = module.TitleEl;
        moduleDescriptionEn = module.DescriptionEn;
        moduleDescriptionEl = module.DescriptionEl;
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
            var request = new SaveModuleRequest( moduleTitleEn , moduleTitleEl , moduleDescriptionEn , moduleDescriptionEl , moduleSortOrder , CourseId );

            var result = editingModuleId == null
                ? await ModuleConsumer.CreateModuleAsync( request )
                : await ModuleConsumer.UpdateModuleAsync( editingModuleId.Value , request );

            if ( result is not null )
            {
                await LoadModulesAndLessons();
                CloseModuleModal();
                message = "Module saved";
            }
            else
            {
                message = "Error saving module";
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
            var success = await ModuleConsumer.DeleteModuleAsync( moduleId );
            if ( success )
            {
                lessonsMap.Remove( moduleId );
                await LoadModulesAndLessons();
                message = "Module deleted";
            }
            else
            {
                message = "Error deleting module";
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
        lessonTitleEn = string.Empty;
        lessonTitleEl = null;
        lessonContentEn = null;
        lessonContentEl = null;
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
        lessonTitleEn = lesson.TitleEn;
        lessonTitleEl = lesson.TitleEl;
        lessonContentEn = lesson.ContentEn;
        lessonContentEl = lesson.ContentEl;
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
        if ( string.IsNullOrWhiteSpace( lessonTitleEn ) )
        {
            message = "Lesson title is required.";
            return;
        }

        isLessonSaving = true;
        message = string.Empty;
        try
        {
            var contentEn = lessonContentEditorEn != null
                ? await lessonContentEditorEn.GetContentAsync()
                : lessonContentEn;

            var contentEl = lessonContentEditorEl != null
                ? await lessonContentEditorEl.GetContentAsync()
                : lessonContentEl;

            var request = new SaveLessonRequest(
                lessonTitleEn , lessonTitleEl , contentEn , contentEl , lessonVideoUrl , lessonDuration , lessonSortOrder , lessonModuleId );

            Guid lessonId;

            if ( editingLessonId == null )
            {
                var created = await LessonConsumer.CreateLessonAsync( request );
                if ( created is not null )
                {
                    lessonId = created.Id;
                }
                else
                {
                    message = "Error creating lesson";
                    return;
                }
            }
            else
            {
                lessonId = editingLessonId.Value;
                var updated = await LessonConsumer.UpdateLessonAsync( lessonId , request );
                if ( updated is null )
                {
                    message = "Error updating lesson";
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

            await LoadModulesAndLessons();
            CloseLessonModal();
            message = uploadErrors.Length > 0 ? uploadErrors.ToString().Trim() : "Lesson saved";
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

    private async Task<string?> UploadFileAsync( Guid lessonId , IBrowserFile file , string type )
    {
        var result = type == "pdf"
            ? await LessonConsumer.UploadPdfAsync( lessonId , file )
            : await LessonConsumer.UploadVideoAsync( lessonId , file );
        return result is not null ? null : $"Error uploading {type}";
    }

    private async Task DeleteLesson( Guid lessonId , Guid moduleId )
    {
        if ( !await JSRuntime.InvokeAsync<bool>( "confirm" , "Delete this lesson?" ) )
            return;

        try
        {
            var success = await LessonConsumer.DeleteLessonAsync( lessonId );
            if ( success )
            {
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
            if ( await CourseConsumer.PublishCourseAsync( CourseId ) )
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
            if ( await CourseConsumer.UnpublishCourseAsync( CourseId ) )
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

    private void UpdatePreviewJson() => _coursePreviewJson = GenerateCoursePreviewJson();

    private string GenerateCoursePreviewJson()
    {
        if ( modules == null )
            return "{}";

        var obj = new
        {
            titleEn = courseTitleEn ,
            titleEl = courseTitleEl ,
            descriptionEn = courseDescriptionEn ,
            descriptionEl = courseDescriptionEl ,
            isPublished = course?.IsPublished ?? false ,
            modules = modules.Select( m =>
            {
                var moduleLessons = lessonsMap.GetValueOrDefault( m.Id ) ?? [];
                return new
                {
                    titleEn = m.TitleEn ,
                    titleEl = m.TitleEl ,
                    descriptionEn = m.DescriptionEn ,
                    descriptionEl = m.DescriptionEl ,
                    sortOrder = m.SortOrder ,
                    lessons = moduleLessons.Select( l =>
                    {
                        var dict = new Dictionary<string , object?>
                        {
                            [ "titleEn" ] = l.TitleEn ,
                            [ "titleEl" ] = l.TitleEl ,
                            [ "sortOrder" ] = l.SortOrder
                        };

                        if ( !string.IsNullOrEmpty( l.VideoPath ) )
                            dict [ "videoPath" ] = l.VideoPath;

                        if ( !string.IsNullOrEmpty( l.ContentEn ) )
                        {
                            var contentPreview = l.ContentEn.Length > 80
                                ? l.ContentEn [ ..80 ] + "..."
                                : l.ContentEn;
                            dict [ "contentEn" ] = contentPreview;
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

        return JsonSerializer.Serialize( obj , _previewJsonOptions );
    }
}
