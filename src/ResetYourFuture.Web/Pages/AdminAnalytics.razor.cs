using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class AdminAnalytics
{
    [Inject] private IAdminAnalyticsConsumer Analytics { get; set; } = default!;

    private AnalyticsSummaryDto? stats;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            stats = await Analytics.GetSummaryAsync();
        }
        catch ( Exception ex )
        {
            Console.WriteLine( $"Error loading analytics: {ex.Message}" );
        }
    }
}
