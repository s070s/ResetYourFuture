using Microsoft.AspNetCore.Components;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Consumers;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class Paths
{
    [Inject] private IPathConsumer PathConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private List<LearningPathListItemDto> _paths = [];
    private bool _loading = true;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnInitializedAsync()
    {
        _paths = [.. await PathConsumer.GetPathsAsync(CurrentLang)];
        _loading = false;
    }

    private void ViewPath(Guid id) => Navigation.NavigateTo($"/paths/{id}");
}
