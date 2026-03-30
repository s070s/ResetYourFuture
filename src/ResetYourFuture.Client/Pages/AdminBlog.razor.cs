using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class AdminBlog : IAsyncDisposable
{
    [Inject] private IAdminBlogConsumer BlogConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PagedResult<AdminBlogArticleDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50, 100];
    private string searchTerm = string.Empty;
    private string message = string.Empty;
    private Guid? confirmDeleteId;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnInitializedAsync()
    {
        await LoadArticles();
    }

    private async Task LoadArticles()
    {
        pagedResult = await BlogConsumer.GetArticlesAsync(
            currentPage,
            pageSize,
            string.IsNullOrEmpty( searchTerm ) ? null : searchTerm );
    }

    private async Task OnSearchInput( ChangeEventArgs e )
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        currentPage = 1;

        var previous = _searchCts;
        _searchCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay( 300, _searchCts.Token );
            await LoadArticles();
        }
        catch ( OperationCanceledException ) { }
    }

    private async Task OnPageSizeChanged( int size )
    {
        pageSize = size;
        currentPage = 1;
        await LoadArticles();
    }

    private async Task PreviousPage()
    {
        if ( currentPage > 1 )
        {
            currentPage--;
            await LoadArticles();
        }
    }

    private async Task NextPage()
    {
        if ( pagedResult is { HasNextPage: true } )
        {
            currentPage++;
            await LoadArticles();
        }
    }

    private void NewArticle() => Navigation.NavigateTo( "/admin/blog/new" );

    private void EditArticle( Guid id ) => Navigation.NavigateTo( $"/admin/blog/{id}" );

    private async Task TogglePublish( AdminBlogArticleDto article )
    {
        try
        {
            bool success;
            if ( article.IsPublished )
                success = await BlogConsumer.UnpublishArticleAsync( article.Id );
            else
                success = await BlogConsumer.PublishArticleAsync( article.Id );

            if ( success )
            {
                message = article.IsPublished ? "Article unpublished" : "Article published";
                await LoadArticles();
            }
            else
            {
                message = "Failed to update publish status";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteArticle( Guid id )
    {
        try
        {
            var success = await BlogConsumer.DeleteArticleAsync( id );
            if ( success )
            {
                confirmDeleteId = null;
                message = "Article deleted";
                await LoadArticles();
            }
            else
            {
                message = "Error deleting article";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
