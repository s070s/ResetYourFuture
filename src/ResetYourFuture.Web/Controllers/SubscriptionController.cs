using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ResetYourFuture.Application.Common;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Application.ApiInterfaces;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Subscription management endpoints.
/// Plans listing is public; status and checkout require authentication.
/// </summary>
[ApiController]
[Route("api/subscriptions")]
[Authorize]
[Tags("Subscriptions")]
[Produces("application/json", "application/problem+json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionController> _logger;
    private readonly PaymentOptions _paymentOptions;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ILogger<SubscriptionController> logger,
        IOptions<PaymentOptions> paymentOptions)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
        _paymentOptions = paymentOptions.Value;
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
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<CheckoutSessionDto>> CreateCheckout(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _subscriptionService.CreateCheckoutSessionAsync(
            UserId, request.PlanId, cancellationToken);

        if (string.IsNullOrEmpty(session.SessionId))
            return BadRequest(session);

        // NOTE: when Payment:MockEnabled is off
        // (the production default), no real payment provider is wired, so checkout cannot complete
        // and returns 503. In Development MockEnabled=true assigns the plan instantly, no charge.
        if (session.Status == "pending_payment")
            return Problem(
                detail: "Payment processing is not yet available. Please check back later.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        _logger.LogInformation(
            "Checkout session {SessionId} created for user {UserId}",
            session.SessionId, UserId);

        return Ok(session);
    }

    /// <summary>
    /// Stripe webhook handler.
    /// Verifies the HMAC-SHA256 signature from the Stripe-Signature header before processing.
    /// Fails closed (SEC-4) when Payment:WebhookSecret is not configured — this endpoint is
    /// [AllowAnonymous], so skipping verification would let anyone POST a "verified" 200 ack;
    /// harmless today only because event dispatch below is not yet implemented.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Seek(0, SeekOrigin.Begin);

        var webhookSecret = _paymentOptions.WebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogWarning("Stripe webhook received but Payment:WebhookSecret is not configured — rejecting.");
            return Problem(
                detail: "Webhook signing secret is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
            return Problem(detail: "Missing Stripe-Signature header.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!VerifyStripeSignature(rawBody, signatureHeader, webhookSecret, out var timestamp))
        {
            _logger.LogWarning("Stripe webhook signature verification failed.");
            return Problem(detail: "Invalid webhook signature.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Reject replayed events older than 5 minutes
        var eventAge = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (eventAge.TotalMinutes > 5)
        {
            _logger.LogWarning("Stripe webhook event is too old ({Age:F0} min) — possible replay attack.", eventAge.TotalMinutes);
            return Problem(detail: "Webhook event timestamp is too old.", statusCode: StatusCodes.Status400BadRequest);
        }

        // NOTE: signature verification is implemented,
        // but event dispatch is not. Until it is, a real Stripe payment would be verified here and
        // then NOT activate any plan. Implement before going live:
        //   checkout.session.completed        → AssignPlanAsync
        //   customer.subscription.updated     → update tier
        //   customer.subscription.deleted     → revert to Free
        _logger.LogInformation("Stripe webhook verified. Event processing not yet implemented.");
        return Ok(new StripeWebhookAckDto(true));
    }

    // Implements Stripe's HMAC-SHA256 signature scheme:
    // https://stripe.com/docs/webhooks/signatures
    private static bool VerifyStripeSignature(string rawBody, string signatureHeader, string secret, out long timestamp)
    {
        timestamp = 0;

        // Header format: "t=<unix_ts>,v1=<hex_sig>[,v1=<hex_sig>...]"
        string? timestampStr = null;
        var v1Signatures = new List<string>();

        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;
            if (kv[0] == "t")
                timestampStr = kv[1];
            else if (kv[0] == "v1")
                v1Signatures.Add(kv[1]);
        }

        if (timestampStr is null || !long.TryParse(timestampStr, out timestamp) || v1Signatures.Count == 0)
            return false;

        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(signedPayload)).ToLowerInvariant();

        return v1Signatures.Any(sig =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(sig)));
    }

    /// <summary>
    /// Cancel the current paid subscription and revert to the Free plan.
    /// </summary>
    [HttpPost("cancel")]
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
    /// Get billing overview: current plan + paged transaction history.
    /// </summary>
    [HttpGet("billing")]
    public async Task<ActionResult<BillingOverviewDto>> GetBillingOverview(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "createdat",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var overview = await _subscriptionService.GetBillingOverviewAsync(UserId, page, pageSize, sortBy, sortDir, cancellationToken);
        return Ok(overview);
    }
}
