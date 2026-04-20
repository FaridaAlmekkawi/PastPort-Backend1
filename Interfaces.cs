// ============================================================
//  ISubscriptionService.cs & IPaymentService.cs — Interfaces
// ============================================================
using SubscriptionPayment.Application.DTOs;

namespace SubscriptionPayment.Application.Interfaces;

// ──────────────────────────────────────────────────────────
// SUBSCRIPTION SERVICE
// ──────────────────────────────────────────────────────────
public interface ISubscriptionService
{
    /// <summary>Returns all active, public plans with their features.</summary>
    Task<IEnumerable<PlanDto>> GetActivePlansAsync(CancellationToken ct = default);

    /// <summary>Returns a single plan by ID.</summary>
    Task<PlanDto?> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Returns the user's current active subscription, if any.</summary>
    Task<UserSubscriptionDto?> GetActiveSubscriptionAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Step 1 of checkout: create a PendingPayment subscription + transaction
    /// and return the gateway payment URL to redirect the user to.
    /// </summary>
    Task<InitiateCheckoutResponse> InitiateCheckoutAsync(
        string userId,
        InitiateCheckoutRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Called after a successful webhook to activate the subscription.
    /// </summary>
    Task ActivateSubscriptionAsync(Guid transactionId, CancellationToken ct = default);

    /// <summary>
    /// Called after a failed webhook to mark the subscription as PastDue.
    /// </summary>
    Task HandleFailedPaymentAsync(Guid transactionId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Upgrades or downgrades a user's plan, applying proration if requested.
    /// </summary>
    Task<UserSubscriptionDto> ChangePlanAsync(
        string userId,
        UpgradePlanRequest request,
        CancellationToken ct = default);

    /// <summary>Cancels a subscription at period end (does not revoke access immediately).</summary>
    Task CancelSubscriptionAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Feature access gate: returns true if the user's active plan includes the feature slug.
    /// </summary>
    Task<bool> HasFeatureAccessAsync(string userId, string featureSlug, CancellationToken ct = default);
}

// ──────────────────────────────────────────────────────────
// PAYMENT SERVICE
// ──────────────────────────────────────────────────────────
public interface IPaymentService
{
    /// <summary>
    /// Creates a checkout session on the gateway (Stripe, Paymob, etc.)
    /// Returns the redirect URL and gateway transaction ID.
    /// </summary>
    Task<(string PaymentUrl, string GatewayTransactionId)> CreateCheckoutSessionAsync(
        Guid subscriptionId,
        Guid transactionId,
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies the webhook signature and parses the raw body into a
    /// normalized PaymentWebhookEvent. Throws if signature is invalid.
    /// </summary>
    Task<PaymentWebhookEvent> ParseAndVerifyWebhookAsync(
        string rawBody,
        string signatureHeader,
        CancellationToken ct = default);

    /// <summary>
    /// Main webhook dispatcher: logs the event, checks idempotency,
    /// and delegates to SubscriptionService for state changes.
    /// </summary>
    Task ProcessWebhookAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default);

    /// <summary>Returns the transaction history for a user.</summary>
    Task<IEnumerable<TransactionDto>> GetTransactionHistoryAsync(
        string userId,
        CancellationToken ct = default);

    /// <summary>Returns all invoices for a user.</summary>
    Task<IEnumerable<InvoiceDto>> GetInvoicesAsync(string userId, CancellationToken ct = default);
}

// ──────────────────────────────────────────────────────────
// GATEWAY ADAPTER — one implementation per gateway
// ──────────────────────────────────────────────────────────
public interface IPaymentGatewayAdapter
{
    Task<(string PaymentUrl, string GatewayTransactionId)> CreateSessionAsync(
        Guid transactionId,
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    PaymentWebhookEvent ParseWebhookPayload(string rawBody, string signatureHeader);
}