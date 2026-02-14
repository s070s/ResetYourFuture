using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Shared.Subscriptions;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Subscription management endpoints.
/// Plans listing is public; status and checkout require authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    /// <summary>
    /// Get all active subscription plans with features.
    /// Public endpoint for pricing page.
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<List<SubscriptionPlanDto>>> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await _subscriptionService.GetPlansAsync(cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// Get current user's subscription status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<UserSubscriptionStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _subscriptionService.GetUserStatusAsync(UserId, cancellationToken);
        return Ok(status);
    }

    /// <summary>
    /// Create a checkout session for a plan (test mode stub).
    /// In production, this would redirect to Stripe Checkout.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize]
    public async Task<ActionResult<CheckoutSessionDto>> CreateCheckout(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _subscriptionService.CreateCheckoutSessionAsync(
            UserId, request.PlanId, cancellationToken);

        if (string.IsNullOrEmpty(session.SessionId))
        {
            return BadRequest(session);
        }

        _logger.LogInformation(
            "Checkout session {SessionId} created for user {UserId}",
            session.SessionId, UserId);

        return Ok(session);
    }

    /// <summary>
    /// Stripe webhook handler (stubbed).
    /// In production, this would verify Stripe signatures and process events.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        // --- STUB: In production, read the request body, verify Stripe signature,
        // and process events like checkout.session.completed, customer.subscription.updated, etc.
        _logger.LogInformation("Stub webhook endpoint called. No processing in test mode.");

        await Task.CompletedTask;
        return Ok(new { received = true });
    }

    /// <summary>
    /// Cancel the current paid subscription and revert to the Free plan.
    /// </summary>
    [HttpPost("cancel")]
    [Authorize]
    public async Task<ActionResult<CancelSubscriptionResultDto>> CancelSubscription(CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.CancelSubscriptionAsync(UserId, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        _logger.LogInformation("User {UserId} cancelled their subscription.", UserId);
        return Ok(result);
    }

    /// <summary>
    /// Get billing overview: current plan + transaction history.
    /// </summary>
    [HttpGet("billing")]
    [Authorize]
    public async Task<ActionResult<BillingOverviewDto>> GetBillingOverview(CancellationToken cancellationToken)
    {
        var overview = await _subscriptionService.GetBillingOverviewAsync(UserId, cancellationToken);
        return Ok(overview);
    }
}
