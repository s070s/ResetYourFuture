using Microsoft.AspNetCore.Components;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Consumers;
using System.Globalization;

namespace ResetYourFuture.Web.Pages;

public partial class PathDetail
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IPathConsumer PathConsumer { get; set; } = default!;

    private LearningPathDetailDto? _path;
    private bool _loading = true;

    private static string CurrentLang =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el" ? "el" : "en";

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _path = await PathConsumer.GetPathAsync(Id, CurrentLang);
        _loading = false;
    }
}
