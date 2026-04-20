using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Application.DTOs;
using PastPort.Infrastructure.Data;


namespace PastPort.Infrastructure.Identity
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _db; // تم تغيير الاسم هنا
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentGatewayAdapter _gatewayAdapter;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            ApplicationDbContext db,
            ISubscriptionService subscriptionService,
            IPaymentGatewayAdapter gatewayAdapter,
            ILogger<PaymentService> logger)
        {
            _db = db;
            _subscriptionService = subscriptionService;
            _gatewayAdapter = gatewayAdapter;
            _logger = logger;
        }

        public Task<(string PaymentUrl, string GatewayTransactionId)> CreateCheckoutSessionAsync(
            Guid subscriptionId, Guid transactionId,
            decimal amount, string currency,
            string successUrl, string cancelUrl,
            CancellationToken ct = default)
            => _gatewayAdapter.CreateSessionAsync(
                transactionId, amount, currency, successUrl, cancelUrl, ct);

        public Task<PaymentWebhookEvent> ParseAndVerifyWebhookAsync(
            string rawBody, string signatureHeader, CancellationToken ct = default)
        {
            var evt = _gatewayAdapter.ParseWebhookPayload(rawBody, signatureHeader);
            return Task.FromResult(evt);
        }

        public async Task ProcessWebhookAsync(
            PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Webhook received. Gateway={Gateway} EventId={EventId} Type={Type}",
                webhookEvent.Gateway, webhookEvent.GatewayEventId, webhookEvent.EventType);

            // ── Idempotency check ─────────────────────────────────
            var existingLog = await _db.WebhookLogs
                .FirstOrDefaultAsync(w =>
                    w.Gateway == webhookEvent.Gateway &&
                    w.GatewayEventId == webhookEvent.GatewayEventId, ct);

            if (existingLog != null && existingLog.Processed)
            {
                _logger.LogWarning("Duplicate webhook {EventId} skipped.", webhookEvent.GatewayEventId);
                return;
            }

            // ── Log or Update the event ───────────────────────────
            var log = existingLog ?? new WebhookLog
            {
                Gateway = webhookEvent.Gateway,
                GatewayEventId = webhookEvent.GatewayEventId,
                EventType = webhookEvent.EventType,
                Payload = webhookEvent.RawPayload
            };

            if (existingLog == null) _db.WebhookLogs.Add(log);
            await _db.SaveChangesAsync(ct);

            try
            {
                var transaction = await _db.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.GatewayTransactionId == webhookEvent.GatewayTransactionId, ct);

                if (transaction is null)
                {
                    _logger.LogWarning("No transaction found for GatewayTxId={GatewayTxId}", webhookEvent.GatewayTransactionId);
                    return;
                }

                switch (webhookEvent.TransactionStatus)
                {
                    case PastPort.Domain.Entities.TransactionStatus.Success:
                        await _subscriptionService.ActivateSubscriptionAsync(transaction.Id, ct);
                        break;

                    case TransactionStatus.Failed:
                        await _subscriptionService.HandleFailedPaymentAsync(
                            transaction.Id,
                            webhookEvent.FailureReason ?? "Payment declined",
                            ct);
                        break;

                    case TransactionStatus.Refunded:
                        transaction.Status = TransactionStatus.Refunded;
                        await _db.SaveChangesAsync(ct);
                        break;
                }

                log.Processed = true;
                log.ProcessedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook {EventId}", webhookEvent.GatewayEventId);
                log.ProcessingError = ex.Message;
                await _db.SaveChangesAsync(ct);
                throw;
            }
        }

        // ... بقية الدوال (GetTransactionHistoryAsync و GetInvoicesAsync) تظل كما هي مع تغيير _db ...
        // تأكد من تغيير return txs.Select ليتوافق مع الـ DTO الخاص بك
    }

    // --- ملحوظة لـ StripeGatewayAdapter ---
    // تأكد من أن الـ DTO "PaymentWebhookEvent" يقبل الـ Enum "PaymentGateway.Stripe"
}