using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Shared;
using ResetYourFuture.Shared.DTOs;
using System.Text.RegularExpressions;

namespace ResetYourFuture.Web.Pages;

public partial class AdminBlogEditor
{
    [Parameter] public Guid? Id { get; set; }

    [Inject] private IAdminBlogConsumer BlogConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _isEditMode => Id.HasValue;
    private bool _isBusy;
    private string? _error;
    private bool _slugManuallyEdited;

    // Form fields — English (required)
    private string _titleEn = string.Empty;
    private string _summaryEn = string.Empty;
    private string _contentEn = string.Empty;

    // Form fields — Greek (optional, falls back to EN)
    private string _titleEl = string.Empty;
    private string _summaryEl = string.Empty;
    private string _contentEl = string.Empty;

    // Shared fields
    private string _slug = string.Empty;
    private string? _coverImageUrl;
    private string _authorName = string.Empty;
    private string _tagsInput = string.Empty;
    private bool _isPublished;

    // Quill editor references
    private QuillEditor? _editorEn;
    private QuillEditor? _editorEl;

    // Pending cover image file (for new articles — uploaded after initial save)
    private IBrowserFile? _pendingCoverFile;

    protected override async Task OnParametersSetAsync()
    {
        if ( _isEditMode )
        {
            var article = await BlogConsumer.GetArticleAsync( Id!.Value );
            if ( article is not null )
            {
                _titleEn      = article.TitleEn;
                _titleEl      = article.TitleEl ?? string.Empty;
                _slug         = article.Slug;
                _summaryEn    = article.SummaryEn;
                _summaryEl    = article.SummaryEl ?? string.Empty;
                _contentEn    = article.ContentEn;
                _contentEl    = article.ContentEl ?? string.Empty;
                _coverImageUrl = article.CoverImageUrl;
                _authorName   = article.AuthorName;
                _tagsInput    = string.Join( ", ", article.Tags );
                _isPublished  = article.IsPublished;
                _slugManuallyEdited = true;
            }
        }
    }

    private void OnTitleEnInput( ChangeEventArgs e )
    {
        _titleEn = e.Value?.ToString() ?? string.Empty;
        if ( !_slugManuallyEdited )
            _slug = GenerateSlug( _titleEn );
    }

    private void OnSlugInput( ChangeEventArgs e )
    {
        _slug = e.Value?.ToString() ?? string.Empty;
        _slugManuallyEdited = true;
    }

    private async Task OnCoverFileSelected( InputFileChangeEventArgs e )
    {
        var file = e.File;
        if ( file is null ) return;

        if ( _isEditMode )
        {
            // In edit mode: upload immediately
            _isBusy = true;
            _error = null;
            try
            {
                var path = await BlogConsumer.UploadCoverImageAsync( Id!.Value, file );
                if ( path is not null )
                    _coverImageUrl = path;
                else
                    _error = "Cover image upload failed.";
            }
            finally
            {
                _isBusy = false;
            }
        }
        else
        {
            // In create mode: hold file, upload after article is saved
            _pendingCoverFile = file;
            _coverImageUrl = file.Name; // Show filename as placeholder
        }
    }

    private void RemoveCover()
    {
        _coverImageUrl = null;
        _pendingCoverFile = null;
    }

    private async Task Save()
    {
        _isBusy = true;
        _error = null;

        try
        {
            // Read latest content from Quill editors
            var contentEn = _editorEn is not null ? await _editorEn.GetContentAsync() : _contentEn;
            var contentEl = _editorEl is not null ? await _editorEl.GetContentAsync() : _contentEl;

            var tags = string.IsNullOrWhiteSpace( _tagsInput )
                ? null
                : _tagsInput.Split( ',' , StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );

            // For new articles with a pending file, don't send the filename as the URL
            var coverUrl = _pendingCoverFile is not null && !_isEditMode
                ? null
                : _coverImageUrl;

            var request = new SaveBlogArticleRequest(
                TitleEn: _titleEn,
                TitleEl: string.IsNullOrWhiteSpace( _titleEl ) ? null : _titleEl,
                Slug: _slug,
                SummaryEn: _summaryEn,
                SummaryEl: string.IsNullOrWhiteSpace( _summaryEl ) ? null : _summaryEl,
                ContentEn: contentEn ?? string.Empty,
                ContentEl: string.IsNullOrWhiteSpace( contentEl ) ? null : contentEl,
                CoverImageUrl: string.IsNullOrWhiteSpace( coverUrl ) ? null : coverUrl,
                AuthorName: _authorName,
                Tags: tags,
                IsPublished: _isPublished );

            AdminBlogArticleDto? result;

            if ( _isEditMode )
                result = await BlogConsumer.UpdateArticleAsync( Id!.Value, request );
            else
                result = await BlogConsumer.CreateArticleAsync( request );

            if ( result is null )
            {
                _error = "Slug already in use. Please choose a different slug.";
                return;
            }

            // Upload pending cover image for newly created articles
            if ( _pendingCoverFile is not null && !_isEditMode )
            {
                await BlogConsumer.UploadCoverImageAsync( result.Id, _pendingCoverFile );
            }

            Navigation.NavigateTo( "/admin/blog" );
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

    private void Cancel() => Navigation.NavigateTo( "/admin/blog" );

    private static string GenerateSlug( string title )
    {
        if ( string.IsNullOrWhiteSpace( title ) )
            return string.Empty;

        var slug = title.Trim().ToLowerInvariant();
        slug = Regex.Replace( slug, @"\s+", "-" );
        slug = Regex.Replace( slug, @"[^a-z0-9\-]", string.Empty );
        slug = Regex.Replace( slug, @"-{2,}", "-" ).Trim( '-' );
        return slug;
    }
}
