using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Consumers;

namespace ResetYourFuture.Web.Pages;

public partial class Search
{
    private const int PageLimit = 20;

    [Inject] private ISearchConsumer Consumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "q")]
    private string? Q { get; set; }

    private string _input = string.Empty;
    private bool _loading;
    private SiteSearchResultDto? _result;
    private string? _loadedFor;

    private static string Lang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnParametersSetAsync()
    {
        _input = Q ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Q) || Q == _loadedFor)
            return;

        _loadedFor = Q;
        _loading = true;
        _result = null;
        StateHasChanged();

        _result = await Consumer.SearchAsync(Q, PageLimit, Lang);
        _loading = false;
    }

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && _input.Trim().Length > 0)
            Navigation.NavigateTo($"/search?q={Uri.EscapeDataString(_input.Trim())}");
    }

    private static string IconFor(string sourceType) => sourceType switch
    {
        "Course" => "bi-book",
        "Assessment" => "bi-list-check",
        "BlogArticle" => "bi-newspaper",
        _ => "bi-file-earmark"
    };

    private static string TypeLabel(string sourceType) => sourceType switch
    {
        "Course" => SearchRes.TypeCourse,
        "Assessment" => SearchRes.TypeAssessment,
        "BlogArticle" => SearchRes.TypeBlogArticle,
        _ => sourceType
    };
}
