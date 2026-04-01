using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminTestimonialEditor
{
    [Parameter] public Guid? Id { get; set; }

    [Inject] private IAdminTestimonialConsumer TestimonialConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _isEditMode => Id.HasValue;
    private bool _isBusy;
    private string? _error;

    // Form fields
    private string _fullName = string.Empty;
    private string _roleOrTitle = string.Empty;
    private string _companyOrContext = string.Empty;
    private string _quoteText = string.Empty;
    private int _displayOrder = 1;
    private bool _isActive = true;

    // Avatar state
    private string? _avatarPath;
    private string? _avatarPreviewUrl;

    /// <summary>Fallback initials shown when no avatar is set.</summary>
    private string Initials => string.IsNullOrWhiteSpace( _fullName )
        ? "?"
        : string.Concat( _fullName.Split( ' ', StringSplitOptions.RemoveEmptyEntries )
                                  .Take( 2 )
                                  .Select( w => char.ToUpperInvariant( w[0] ) ) );

    protected override async Task OnParametersSetAsync()
    {
        if ( _isEditMode )
        {
            var item = await TestimonialConsumer.GetByIdAsync( Id!.Value );
            if ( item is not null )
            {
                _fullName        = item.FullName;
                _roleOrTitle     = item.RoleOrTitle ?? string.Empty;
                _companyOrContext = item.CompanyOrContext ?? string.Empty;
                _quoteText       = item.QuoteText;
                _displayOrder    = item.DisplayOrder;
                _isActive        = item.IsActive;
                _avatarPath      = item.AvatarPath;
                _avatarPreviewUrl = BuildPreviewUrl( item.AvatarPath );
            }
        }
    }

    private async Task OnAvatarFileSelected( InputFileChangeEventArgs e )
    {
        var file = e.File;
        if ( file is null ) return;

        _isBusy = true;
        _error  = null;
        try
        {
            var path = await TestimonialConsumer.UploadAvatarAsync( Id!.Value, file );
            if ( path is not null )
            {
                _avatarPath       = path;
                _avatarPreviewUrl = BuildPreviewUrl( path );
            }
            else
            {
                _error = "Avatar upload failed. Check file type and size (max 5 MB).";
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task RemoveAvatar()
    {
        _isBusy = true;
        _error  = null;
        try
        {
            var success = await TestimonialConsumer.RemoveAvatarAsync( Id!.Value );
            if ( success )
            {
                _avatarPath       = null;
                _avatarPreviewUrl = null;
            }
            else
            {
                _error = "Failed to remove avatar.";
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task Save()
    {
        _error = null;

        if ( string.IsNullOrWhiteSpace( _fullName ) )
        {
            _error = "Full Name is required.";
            return;
        }
        if ( string.IsNullOrWhiteSpace( _quoteText ) )
        {
            _error = "Quote is required.";
            return;
        }

        _isBusy = true;
        try
        {
            var request = new SaveTestimonialRequest(
                FullName:         _fullName.Trim(),
                RoleOrTitle:      string.IsNullOrWhiteSpace( _roleOrTitle ) ? null : _roleOrTitle.Trim(),
                CompanyOrContext:  string.IsNullOrWhiteSpace( _companyOrContext ) ? null : _companyOrContext.Trim(),
                QuoteText:        _quoteText.Trim(),
                DisplayOrder:     _displayOrder,
                IsActive:         _isActive );

            AdminTestimonialDto? result;

            if ( _isEditMode )
                result = await TestimonialConsumer.UpdateAsync( Id!.Value, request );
            else
                result = await TestimonialConsumer.CreateAsync( request );

            if ( result is null )
            {
                _error = "Failed to save testimonial. Please try again.";
                return;
            }

            Navigation.NavigateTo( "/admin/testimonials" );
        }
        catch ( Exception ex )
        {
            _error = $"Error: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void Cancel() => Navigation.NavigateTo( "/admin/testimonials" );

    /// <summary>
    /// Builds a preview URL for the avatar using the API media endpoint.
    /// IConfiguration is injected globally via _Imports.razor.
    /// </summary>
    private string? BuildPreviewUrl( string? path )
    {
        if ( string.IsNullOrWhiteSpace( path ) )
            return null;

        if ( path.StartsWith( "http", StringComparison.OrdinalIgnoreCase ) )
            return path;

        var apiBase = Configuration["ApiBaseUrl"]?.TrimEnd( '/' ) ?? string.Empty;
        return $"{apiBase}/api/media/{path.TrimStart( '/' )}";
    }
}
