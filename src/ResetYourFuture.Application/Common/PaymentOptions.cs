namespace ResetYourFuture.Application.Common;

/// <summary>
/// Binds the "Payment" configuration section (CFG-5) so the payment keys are discoverable in
/// appsettings.json and bound in one place instead of read ad hoc via IConfiguration.
/// </summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// Development-only mock checkout that grants the plan without a real payment. The BIZ-4 guard
    /// additionally requires the Development environment at runtime, so this flag alone can never
    /// grant free upgrades in a real deployment.
    /// </summary>
    public bool MockEnabled { get; set; }

    /// <summary>
    /// Stripe webhook signing secret. When unset the webhook endpoint fails closed (503) rather
    /// than skipping signature verification (SEC-4).
    /// </summary>
    public string? WebhookSecret { get; set; }
}
