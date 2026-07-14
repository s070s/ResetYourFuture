using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Shared.Components.Forms;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

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
    [Inject] private IAdminCategoryConsumer CategoryConsumer { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool IsNew => CourseId == Guid.Empty;
    private AdminCourseDto? course;
    private List<AdminModuleDto>? modules;
    private Dictionary<Guid, List<AdminLessonDto>> lessonsMap = new();
    private HashSet<Guid> expandedModules = new();
    private bool isSaving;
    private string message = string.Empty;
    private string messageType = "success";
    private bool _isDirty;

    private void MarkDirty() => _isDirty = true;

    // Delete confirmation state
    private Guid? _pendingDeleteModuleId;
    private Guid? _pendingDeleteLessonId;
    private Guid? _pendingDeleteLessonModuleId;
    private string _pendingDeleteMessage = string.Empty;

    // Course form fields
    private string courseTitleEn = string.Empty;
    private string? courseTitleEl;
    private string? courseDescriptionEn;
    private string? courseDescriptionEl;
    private SubscriptionTier courseRequiredTier = SubscriptionTier.Free;
    private QuillEditor? descriptionEditorEn;
    private QuillEditor? descriptionEditorEl;

    // Category picker state
    private List<CategoryOptionDto> categories = [];
    private Guid? courseCategoryId;
    private bool creatingNewCategory;
    private string? newCategoryName;

    // Module modal state
    private bool _showModuleModal;
    private AdminModuleDto? _editingModule;

    // Lesson modal state
    private bool _showLessonModal;
    private AdminLessonDto? _editingLesson;
    private Guid _lessonModalModuleId;
    private int _lessonModalDefaultSortOrder;

    private Guid _lastLoadedCourseId;

    protected override async Task OnParametersSetAsync()
    {
        // Guard: only reload when CourseId actually changes (handles same-component navigation)
        if (CourseId == _lastLoadedCourseId)
            return;

        _lastLoadedCourseId = CourseId;

        // Reset state for the new course
        course = null;
        modules = null;
        lessonsMap = new();
        expandedModules = new();
        message = string.Empty;
        _isDirty = false;

        await LoadCategories();

        if (!IsNew)
        {
            await LoadCourse();
            await LoadModulesAndLessons();
        }
    }

    // ── Data loading ──

    private async Task LoadCategories()
    {
        try
        {
            categories = await CategoryConsumer.GetAllCategoriesAsync();
        }
        catch
        {
            categories = [];
        }
    }

    private async Task LoadCourse()
    {
        try
        {
            course = await CourseConsumer.GetCourseAsync(CourseId);
            if (course != null)
            {
                courseTitleEn = course.TitleEn;
                courseTitleEl = course.TitleEl;
                courseDescriptionEn = course.DescriptionEn;
                courseDescriptionEl = course.DescriptionEl;
                courseRequiredTier = course.RequiredTier;
                courseCategoryId = course.CategoryId;
                creatingNewCategory = false;
                newCategoryName = null;
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    // ── Category picker ──

    private string CategorySelectValue => creatingNewCategory ? "new" : (courseCategoryId?.ToString() ?? "none");

    private void OnCategorySelectChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (value == "new")
        {
            creatingNewCategory = true;
            courseCategoryId = null;
        }
        else if (string.IsNullOrEmpty(value) || value == "none")
        {
            creatingNewCategory = false;
            courseCategoryId = null;
            newCategoryName = null;
        }
        else
        {
            creatingNewCategory = false;
            newCategoryName = null;
            courseCategoryId = Guid.Parse(value);
        }
    }

    private async Task LoadModulesAndLessons()
    {
        try
        {
            modules = await ModuleConsumer.GetModulesByCourseAsync(CourseId);
            lessonsMap = new();
            if (modules != null)
            {
                modules = [.. modules.OrderBy(m => m.SortOrder)];
                await Task.WhenAll(modules.Select(m => LoadLessonsForModule(m.Id)));
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task LoadLessonsForModule(Guid moduleId)
    {
        try
        {
            var lessons = await LessonConsumer.GetLessonsByModuleAsync(moduleId);
            lessonsMap[moduleId] = [.. lessons.OrderBy(l => l.SortOrder)];
        }
        catch
        {
            lessonsMap[moduleId] = [];
        }
    }

    private void ToggleModule(Guid moduleId)
    {
        if (!expandedModules.Remove(moduleId))
        {
            expandedModules.Add(moduleId);
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

            var categoryIdArg = creatingNewCategory ? null : courseCategoryId;
            var newCategoryNameArg = creatingNewCategory ? newCategoryName : null;

            var request = new SaveCourseRequest(courseTitleEn, courseTitleEl, descEn, descEl, courseRequiredTier, categoryIdArg, newCategoryNameArg);

            if (IsNew)
            {
                var created = await CourseConsumer.CreateCourseAsync(request);
                if (created is not null)
                {
                    _isDirty = false;
                    Nav.NavigateTo($"/admin/courses/{created.Id}");
                    return;
                }
                message = AdminRes.CourseSaveFailed;
                messageType = "danger";
            }
            else
            {
                var updated = await CourseConsumer.UpdateCourseAsync(CourseId, request);
                if (updated is not null)
                {
                    await LoadCourse();
                    await LoadCategories();
                    message = AdminRes.CourseSaved;
                    messageType = "success";
                    _isDirty = false;
                }
                else
                {
                    message = AdminRes.CourseSaveFailed;
                messageType = "danger";
                }
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

    private void Cancel() => Nav.NavigateTo("/admin/courses");

    // ── Module modal ──

    private void ShowAddModule()
    {
        _editingModule = null;
        _showModuleModal = true;
    }

    private void ShowEditModule(AdminModuleDto module)
    {
        _editingModule = module;
        _showModuleModal = true;
    }

    private void CloseModuleModal() => _showModuleModal = false;

    private async Task OnModuleSaved()
    {
        _showModuleModal = false;
        await LoadModulesAndLessons();
        message = AdminRes.ModuleSaved;
        messageType = "success";
    }

    // ── Lesson modal ──

    private void ShowAddLesson(Guid moduleId)
    {
        _editingLesson = null;
        _lessonModalModuleId = moduleId;
        _lessonModalDefaultSortOrder = (lessonsMap.GetValueOrDefault(moduleId)?.Count ?? 0) + 1;
        _showLessonModal = true;
    }

    private void ShowEditLesson(AdminLessonDto lesson)
    {
        _editingLesson = lesson;
        _lessonModalModuleId = lesson.ModuleId;
        _showLessonModal = true;
    }

    private void CloseLessonModal() => _showLessonModal = false;

    private async Task OnLessonSaved()
    {
        _showLessonModal = false;
        await LoadModulesAndLessons();
        message = AdminRes.LessonSaved;
        messageType = "success";
    }

    // ── Delete ──

    private void DeleteModule(Guid moduleId)
    {
        _pendingDeleteModuleId = moduleId;
        _pendingDeleteMessage = AdminRes.DeleteModuleConfirm;
    }

    private void DeleteLesson(Guid lessonId, Guid moduleId)
    {
        _pendingDeleteLessonId = lessonId;
        _pendingDeleteLessonModuleId = moduleId;
        _pendingDeleteMessage = AdminRes.DeleteLessonConfirm;
    }

    private async Task ExecuteDeleteAsync()
    {
        if (_pendingDeleteModuleId is { } moduleId)
        {
            _pendingDeleteModuleId = null;
            _pendingDeleteMessage = string.Empty;
            try
            {
                var success = await ModuleConsumer.DeleteModuleAsync(moduleId);
                if (success)
                {
                    lessonsMap.Remove(moduleId);
                    await LoadModulesAndLessons();
                    message = AdminRes.ModuleDeleted;
                    messageType = "success";
                }
                else
                {
                    message = AdminRes.ModuleDeleteFailed;
                    messageType = "danger";
                }
            }
            catch (Exception ex)
            {
                message = ErrorMessagesRes.UnexpectedErrorTryAgain;
                messageType = "danger";
            }
        }
        else if (_pendingDeleteLessonId is { } lessonId)
        {
            _pendingDeleteLessonId = null;
            _pendingDeleteLessonModuleId = null;
            _pendingDeleteMessage = string.Empty;
            try
            {
                var success = await LessonConsumer.DeleteLessonAsync(lessonId);
                if (success)
                {
                    await LoadModulesAndLessons();
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
    }

    private void CancelPendingDelete()
    {
        _pendingDeleteModuleId = null;
        _pendingDeleteLessonId = null;
        _pendingDeleteLessonModuleId = null;
        _pendingDeleteMessage = string.Empty;
    }

    // ── Publish ──

    private async Task PublishCourse()
    {
        try
        {
            if (await CourseConsumer.PublishCourseAsync(CourseId))
            {
                await LoadCourse();
                message = AdminRes.CoursePublished;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private async Task UnpublishCourse()
    {
        try
        {
            if (await CourseConsumer.UnpublishCourseAsync(CourseId))
            {
                await LoadCourse();
                message = AdminRes.CourseUnpublished;
                messageType = "success";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

}
