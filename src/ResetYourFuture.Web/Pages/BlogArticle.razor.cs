using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class BlogArticle : IDisposable
{
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Inject] private IBlogConsumer BlogConsumer { get; set; } = default!;
    [Inject] private PersistentComponentState ApplicationState { get; set; } = default!;

    private BlogArticleDto? _article;
    private bool _loading = true;
    private bool _notFound;

    private PersistingComponentStateSubscription _persistSub;

    private string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override Task OnInitializedAsync()
    {
        _persistSub = ApplicationState.RegisterOnPersisting( PersistArticle );
        return Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        _loading  = true;
        _notFound = false;
        _article  = null;

        // Restore from prerender state if available (avoids duplicate API call on circuit connect)
        var key = $"blog-article-{Slug}";
        if ( ApplicationState.TryTakeFromJson<BlogArticleDto>( key , out var cached ) )
        {
            _article = cached;
        }
        else
        {
            _article = await BlogConsumer.GetBySlugAsync( Slug , lang: CurrentLang );
        }

        if ( _article is null )
            _notFound = true;

        _loading = false;
    }

    private Task PersistArticle()
    {
        ApplicationState.PersistAsJson( $"blog-article-{Slug}" , _article );
        return Task.CompletedTask;
    }

    public void Dispose() => _persistSub.Dispose();
}
