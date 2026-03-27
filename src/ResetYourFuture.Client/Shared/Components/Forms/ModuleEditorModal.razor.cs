using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Shared.Components.Forms;

public partial class ModuleEditorModal
{
    [Parameter, EditorRequired] public bool IsVisible { get; set; }
    [Parameter] public AdminModuleDto? EditingModule { get; set; }
    [Parameter, EditorRequired] public Guid CourseId { get; set; }
    [Parameter] public int DefaultSortOrder { get; set; } = 1;
    [Parameter, EditorRequired] public EventCallback OnClose { get; set; }
    [Parameter, EditorRequired] public EventCallback OnSaved { get; set; }

    [Inject] private IAdminModuleConsumer ModuleConsumer { get; set; } = default!;

    private string _titleEn = string.Empty;
    private string? _titleEl;
    private string? _descriptionEn;
    private string? _descriptionEl;
    private int _sortOrder;
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

        if ( EditingModule is not null )
        {
            _titleEn = EditingModule.TitleEn;
            _titleEl = EditingModule.TitleEl;
            _descriptionEn = EditingModule.DescriptionEn;
            _descriptionEl = EditingModule.DescriptionEl;
            _sortOrder = EditingModule.SortOrder;
        }
        else
        {
            _titleEn = string.Empty;
            _titleEl = null;
            _descriptionEn = null;
            _descriptionEl = null;
            _sortOrder = DefaultSortOrder;
        }
    }

    private async Task SaveAsync()
    {
        _errorMessage = string.Empty;
        _isSaving = true;
        try
        {
            var request = new SaveModuleRequest( _titleEn , _titleEl , _descriptionEn , _descriptionEl , _sortOrder , CourseId );

            var result = EditingModule is null
                ? await ModuleConsumer.CreateModuleAsync( request )
                : await ModuleConsumer.UpdateModuleAsync( EditingModule.Id , request );

            if ( result is not null )
                await OnSaved.InvokeAsync();
            else
                _errorMessage = "Error saving module.";
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
