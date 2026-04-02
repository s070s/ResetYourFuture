using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminAnalytics
{
    [Inject] private IAdminAnalyticsConsumer Analytics { get; set; } = default!;
    [Inject] private ILogger<AdminAnalytics> _logger { get; set; } = default!;

    private AnalyticsSummaryDto? stats;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            stats = await Analytics.GetSummaryAsync();
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Error loading analytics." );
        }
    }
}
