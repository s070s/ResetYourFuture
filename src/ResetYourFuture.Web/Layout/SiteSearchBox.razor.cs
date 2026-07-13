using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Layout;

public partial class SiteSearchBox : IDisposable
{
    private const int FlyoutLimit = 5;

    [Inject] private ISearchConsumer Consumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string _query = string.Empty;
    private bool _open;
    private bool _loading;
    private SiteSearchResultDto? _result;
    private CancellationTokenSource? _debounceCts;

    private static string Lang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    private void HandleFocus() => _open = true;

    private async Task HandleFocusOut()
    {
        await Task.Delay(200);
        _open = false;
        StateHasChanged();
    }

    private void Close() => _open = false;

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && _query.Trim().Length > 0)
        {
            _open = false;
            Navigation.NavigateTo($"/search?q={Uri.EscapeDataString(_query.Trim())}");
        }
        else if (e.Key == "Escape")
        {
            _open = false;
        }
    }

    private async Task HandleInput(ChangeEventArgs e)
    {
        _query = e.Value?.ToString() ?? string.Empty;
        _open = true;

        var previous = _debounceCts;
        _debounceCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        if (_query.Trim().Length == 0)
        {
            _result = null;
            return;
        }

        try
        {
            await Task.Delay(300, _debounceCts.Token);
            _loading = true;
            StateHasChanged();
            _result = await Consumer.SearchAsync(_query.Trim(), FlyoutLimit, Lang);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a newer keystroke — leave the old cts's state alone
        }
        finally
        {
            _loading = false;
        }
    }

    private static string IconFor(string sourceType) => sourceType switch
    {
        "Course" => "bi-book",
        "Assessment" => "bi-list-check",
        "BlogArticle" => "bi-newspaper",
        _ => "bi-file-earmark"
    };

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }
}
