using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Tags("Subscriptions")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionController> _logger;
    private readonly IConfiguration _configuration;

    public SubscriptionController(
        ISubscriptionService subscriptionService,
        ILogger<SubscriptionController> logger,
        IConfiguration configuration)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
        _configuration = configuration;
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
            return BadRequest(session);

        // NOTE: when Payment:MockEnabled is off
        // (the production default), no real payment provider is wired, so checkout cannot complete
        // and returns 503. In Development MockEnabled=true assigns the plan instantly, no charge.
        if (session.Status == "pending_payment")
            return StatusCode(503, new
            {
                message = "Payment processing is not yet available. Please check back later."
            });

        _logger.LogInformation(
            "Checkout session {SessionId} created for user {UserId}",
            session.SessionId, UserId);

        return Ok(session);
    }

    /// <summary>
    /// Stripe webhook handler.
    /// Verifies the HMAC-SHA256 signature from the Stripe-Signature header before processing.
    /// If Payment:WebhookSecret is not configured the signature check is skipped (dev/no-Stripe mode).
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

        var webhookSecret = _configuration["Payment:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogWarning("Stripe webhook received but Payment:WebhookSecret is not configured — skipping signature check.");
            return Ok(new StripeWebhookAckDto(true));
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
            return BadRequest(new
            {
                error = "Missing Stripe-Signature header."
            });
        }

        if (!VerifyStripeSignature(rawBody, signatureHeader, webhookSecret, out var timestamp))
        {
            _logger.LogWarning("Stripe webhook signature verification failed.");
            return BadRequest(new
            {
                error = "Invalid webhook signature."
            });
        }

        // Reject replayed events older than 5 minutes
        var eventAge = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (eventAge.TotalMinutes > 5)
        {
            _logger.LogWarning("Stripe webhook event is too old ({Age:F0} min) — possible replay attack.", eventAge.TotalMinutes);
            return BadRequest(new
            {
                error = "Webhook event timestamp is too old."
            });
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
    /// Get billing overview: current plan + paged transaction history.
    /// </summary>
    [HttpGet("billing")]
    [Authorize]
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
