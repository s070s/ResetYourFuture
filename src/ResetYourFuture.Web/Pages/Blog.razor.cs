using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Blog : IDisposable
{
    [Inject] private IBlogConsumer BlogConsumer { get; set; } = default!;
    [Inject] private PersistentComponentState ApplicationState { get; set; } = default!;

    private IReadOnlyList<BlogArticleSummaryDto>? _articles;
    private bool _loading = true;

    private PersistingComponentStateSubscription _persistSub;

    private string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        // Reuse the prerendered list on interactive hydration so the client render matches
        // the SSR HTML with no second fetch or flash.
        if (ApplicationState.TryTakeFromJson<List<BlogArticleSummaryDto>>("blog-index", out var restored))
        {
            _articles = restored;
            _loading = false;
        }
        await base.SetParametersAsync(parameters);
    }

    protected override async Task OnInitializedAsync()
    {
        _persistSub = ApplicationState.RegisterOnPersisting(PersistData);

        if (_loading)
        {
            try
            {
                _articles = await BlogConsumer.GetSummariesAsync(count: 50, lang: CurrentLang);
            }
            catch
            {
                // Show the empty state rather than crashing if the blog API is unavailable.
            }
            finally
            {
                _loading = false;
            }
        }
    }

    private Task PersistData()
    {
        ApplicationState.PersistAsJson("blog-index", _articles);
        return Task.CompletedTask;
    }

    public void Dispose() => _persistSub.Dispose();
}
