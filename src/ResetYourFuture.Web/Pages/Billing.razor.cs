using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Pages;

public partial class Billing
{
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Billing> _logger { get; set; } = default!;

    private BillingOverviewDto? _overview;
    private bool _loading = true;
    private bool _cancelling;
    private bool _showCancelConfirm;
    private string? _error;
    private string? _cancelMessage;
    private bool _cancelSuccess;
    private int _page = 1;
    private int _pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string _sortBy = "createdat";
    private string _sortDir = "desc";

    protected override async Task OnInitializedAsync()
    {
        await LoadBillingOverviewAsync();
    }

    private async Task LoadBillingOverviewAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            _overview = await SubscriptionService.GetBillingOverviewAsync(_page, _pageSize, _sortBy, _sortDir);
        }
        catch (Exception ex)
        {
            _error = "Failed to load billing information. Please try again.";
            _logger.LogError(ex, "Failed to load billing overview.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnSort(string columnKey)
    {
        if (_sortBy == columnKey)
            _sortDir = _sortDir == "asc" ? "desc" : "asc";
        else
        {
            _sortBy = columnKey;
            _sortDir = "asc";
        }
        _page = 1;
        await LoadBillingOverviewAsync();
    }

    private async Task PreviousPage()
    {
        if (_overview is { Transactions.HasPreviousPage: true })
        {
            _page--;
            await LoadBillingOverviewAsync();
        }
    }

    private async Task NextPage()
    {
        if (_overview is { Transactions.HasNextPage: true })
        {
            _page++;
            await LoadBillingOverviewAsync();
        }
    }

    private async Task OnPageSizeChanged(int size)
    {
        _pageSize = size;
        _page = 1;
        await LoadBillingOverviewAsync();
    }

    private async Task CancelSubscription()
    {
        _cancelling = true;
        _cancelMessage = null;
        try
        {
            var result = await SubscriptionService.CancelAsync();
            if (result is not null)
            {
                _cancelSuccess = result.Success;
                _cancelMessage = result.Message;

                if (result.Success)
                {
                    _page = 1;
                    await LoadBillingOverviewAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _cancelSuccess = false;
            _cancelMessage = "Failed to cancel. Please try again.";
            _logger.LogError(ex, "Failed to cancel subscription.");
        }
        finally
        {
            _cancelling = false;
            _showCancelConfirm = false;
        }
    }

    private void RequestCancel() => _showCancelConfirm = true;

    private void CloseCancelDialog() => _showCancelConfirm = false;

    private void GoToPricing() => Navigation.NavigateTo("/pricing");
}
