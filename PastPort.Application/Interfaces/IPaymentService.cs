// ──────────────────────────────────────────────────────────
// PAYMENT SERVICE
// ──────────────────────────────────────────────────────────
using PastPort.Application.DTOs;

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