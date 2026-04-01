using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class BlogArticle
{
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Inject] private IBlogConsumer BlogConsumer { get; set; } = default!;

    private BlogArticleDto? _article;
    private bool _loading = true;
    private bool _notFound;

    private string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnParametersSetAsync()
    {
        _loading  = true;
        _notFound = false;
        _article  = null;

        _article = await BlogConsumer.GetBySlugAsync( Slug, lang: CurrentLang );

        if ( _article is null )
            _notFound = true;

        _loading = false;
    }
}
