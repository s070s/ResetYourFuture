using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

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
    private string messageType = "success";
    private Guid? confirmDeleteId;
    private CancellationTokenSource? _searchCts;
    private string _sortBy = "createdat";
    private string _sortDir = "desc";

    protected override async Task OnInitializedAsync()
    {
        await LoadArticles();
    }

    private async Task OnSort(string columnKey)
    {
        if (_sortBy == columnKey)
            _sortDir = _sortDir == "asc" ? "desc" : "asc";
        else
        {
            _sortBy = columnKey;
            _sortDir = "asc";
        }
        currentPage = 1;
        await LoadArticles();
    }

    private async Task LoadArticles()
    {
        pagedResult = await BlogConsumer.GetArticlesAsync(
            currentPage,
            pageSize,
            string.IsNullOrEmpty(searchTerm) ? null : searchTerm,
            _sortBy,
            _sortDir);
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        currentPage = 1;

        var previous = _searchCts;
        _searchCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(300, _searchCts.Token);
            await LoadArticles();
        }
        catch (OperationCanceledException) { }
    }

    private async Task OnPageSizeChanged(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadArticles();
    }

    private async Task PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadArticles();
        }
    }

    private async Task NextPage()
    {
        if (pagedResult is { HasNextPage: true })
        {
            currentPage++;
            await LoadArticles();
        }
    }

    private void NewArticle() => Navigation.NavigateTo("/admin/blog/new");

    private void EditArticle(Guid id) => Navigation.NavigateTo($"/admin/blog/{id}");

    private async Task TogglePublish(AdminBlogArticleDto article)
    {
        try
        {
            bool success;
            if (article.IsPublished)
                success = await BlogConsumer.UnpublishArticleAsync(article.Id);
            else
                success = await BlogConsumer.PublishArticleAsync(article.Id);

            if (success)
            {
                message = article.IsPublished ? AdminRes.ArticleUnpublished : AdminRes.ArticlePublished;
                messageType = "success";
                await LoadArticles();
            }
            else
            {
                message = AdminRes.PublishStatusUpdateFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    private void RequestDeleteArticle(Guid id) => confirmDeleteId = id;

    private void CancelDeleteArticle() => confirmDeleteId = null;

    private async Task DeleteArticle()
    {
        if (confirmDeleteId is not { } id)
            return;

        confirmDeleteId = null;

        try
        {
            var success = await BlogConsumer.DeleteArticleAsync(id);
            if (success)
            {
                message = AdminRes.ArticleDeleted;
                messageType = "success";
                await LoadArticles();
            }
            else
            {
                message = AdminRes.ArticleDeleteFailed;
                messageType = "danger";
            }
        }
        catch (Exception ex)
        {
            message = ErrorMessagesRes.UnexpectedErrorTryAgain;
            messageType = "danger";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
