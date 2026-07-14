using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.Resources.Messages;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminAnalytics
{
    [Inject] private IAdminAnalyticsConsumer Analytics { get; set; } = default!;
    [Inject] private ILogger<AdminAnalytics> _logger { get; set; } = default!;

    private AnalyticsSummaryDto? stats;
    private bool _loading = true;
    private string? _error;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            stats = await Analytics.GetSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics.");
            _error = ErrorMessagesRes.FailedToLoadAnalytics;
        }
        finally
        {
            _loading = false;
        }
    }
}
