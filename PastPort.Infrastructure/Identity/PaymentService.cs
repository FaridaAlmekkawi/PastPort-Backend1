using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;


namespace PastPort.Infrastructure.Identity
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _db;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymentGatewayAdapter _gatewayAdapter;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            AppDbContext db,
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
            // Delegate to the configured adapter (Stripe or Paymob)
            var evt = _gatewayAdapter.ParseWebhookPayload(rawBody, signatureHeader);
            return Task.FromResult(evt);
        }

        // ────────────────────────────────────────────────────────
        // WEBHOOK DISPATCHER
        //
        // Idempotency pattern:
        //   1. Check WebhookLog for duplicate GatewayEventId
        //   2. If already processed → return early (HTTP 200 to gateway)
        //   3. Log event as unprocessed
        //   4. Find our transaction by GatewayTransactionId
        //   5. Dispatch to SubscriptionService based on event type
        //   6. Mark log entry as processed
        // ────────────────────────────────────────────────────────
        public async Task ProcessWebhookAsync(
            PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Webhook received. Gateway={Gateway} EventId={EventId} Type={Type}",
                webhookEvent.Gateway, webhookEvent.GatewayEventId, webhookEvent.EventType);

            // ── Idempotency check ─────────────────────────────────
            var alreadyProcessed = await _db.WebhookLogs
                .AnyAsync(w =>
                    w.Gateway == webhookEvent.Gateway &&
                    w.GatewayEventId == webhookEvent.GatewayEventId &&
                    w.Processed, ct);

            if (alreadyProcessed)
            {
                _logger.LogWarning(
                    "Duplicate webhook. GatewayEventId={EventId} — skipping.",
                    webhookEvent.GatewayEventId);
                return;
            }

            // ── Log the event ─────────────────────────────────────
            var log = new WebhookLog
            {
                Gateway = webhookEvent.Gateway,
                GatewayEventId = webhookEvent.GatewayEventId,
                EventType = webhookEvent.EventType,
                Payload = webhookEvent.RawPayload
            };
            _db.WebhookLogs.Add(log);
            await _db.SaveChangesAsync(ct);

            try
            {
                // ── Find our transaction ──────────────────────────
                var transaction = await _db.PaymentTransactions
                    .FirstOrDefaultAsync(t =>
                        t.GatewayTransactionId == webhookEvent.GatewayTransactionId, ct);

                if (transaction is null)
                {
                    _logger.LogWarning(
                        "No transaction found for GatewayTxId={GatewayTxId}",
                        webhookEvent.GatewayTransactionId);
                    return;
                }

                // ── Dispatch based on normalized event type ───────
                switch (webhookEvent.TransactionStatus)
                {
                    case TransactionStatus.Success:
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
                        // TODO: optionally revoke access or credit the account
                        break;

                    default:
                        _logger.LogWarning(
                            "Unhandled transaction status: {Status}", webhookEvent.TransactionStatus);
                        break;
                }

                // ── Mark webhook as processed ─────────────────────
                log.Processed = true;
                log.ProcessedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook {EventId}", webhookEvent.GatewayEventId);
                log.ProcessingError = ex.Message;
                await _db.SaveChangesAsync(ct);
                throw; // Re-throw so the controller returns 500 → gateway will retry
            }
        }

        public async Task<IEnumerable<TransactionDto>> GetTransactionHistoryAsync(
            string userId, CancellationToken ct = default)
        {
            var txs = await _db.PaymentTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            return txs.Select(t => new TransactionDto(
                t.Id, t.Amount, t.Currency, t.Status, t.Gateway,
                t.GatewayTransactionId, t.FailureReason, t.CreatedAt, t.ProcessedAt));
        }

        public async Task<IEnumerable<InvoiceDto>> GetInvoicesAsync(
            string userId, CancellationToken ct = default)
        {
            var invoices = await _db.Invoices
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.IssuedAt)
                .ToListAsync(ct);

            return invoices.Select(i => new InvoiceDto(
                i.Id, i.InvoiceNumber, i.SubTotal, i.TaxAmount,
                i.DiscountAmount, i.TotalAmount, i.Currency,
                i.Status, i.IssuedAt, i.PaidAt, i.PdfUrl));
        }
    }

    // ──────────────────────────────────────────────────────────
    // STRIPE GATEWAY ADAPTER
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Configuration options loaded from appsettings.json → "Stripe" section.
    /// </summary>
    public class StripeOptions
    {
        public string SecretKey { get; set; } = default!;
        public string WebhookSecret { get; set; } = default!;
        public string PublishableKey { get; set; } = default!;
    }

    public class StripeGatewayAdapter : IPaymentGatewayAdapter
    {
        private readonly StripeOptions _options;
        private readonly ILogger<StripeGatewayAdapter> _logger;

        public StripeGatewayAdapter(
            IOptions<StripeOptions> options,
            ILogger<StripeGatewayAdapter> logger)
        {
            _options = options.Value;
            _logger = logger;
            StripeConfiguration.ApiKey = _options.SecretKey;
        }

        public async Task<(string PaymentUrl, string GatewayTransactionId)> CreateSessionAsync(
            Guid transactionId, decimal amount, string currency,
            string successUrl, string cancelUrl, CancellationToken ct = default)
        {
            var sessionService = new SessionService();
            var sessionOptions = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency    = currency.ToLower(),
                        UnitAmount  = (long)(amount * 100),  // Stripe uses cents
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Subscription"
                        }
                    },
                    Quantity = 1
                }
            },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                // Pass our internal transactionId so we can look it up in the webhook
                Metadata = new Dictionary<string, string>
                {
                    ["transaction_id"] = transactionId.ToString()
                }
            };

            var session = await sessionService.CreateAsync(sessionOptions, cancellationToken: ct);

            _logger.LogInformation(
                "Stripe session created. SessionId={SessionId}", session.Id);

            return (session.Url, session.Id);
        }

        /// <summary>
        /// Validates the Stripe-Signature header and constructs a normalized event.
        /// Throws StripeException if the signature is invalid — controller returns 400.
        /// </summary>
        public PaymentWebhookEvent ParseWebhookPayload(string rawBody, string signatureHeader)
        {
            // This throws if the signature is invalid (tamper protection)
            var stripeEvent = EventUtility.ConstructEvent(
                rawBody, signatureHeader, _options.WebhookSecret);

            var (status, failureReason, gatewayTxId) = stripeEvent.Type switch
            {
                "checkout.session.completed" => ParseSessionCompleted(stripeEvent),
                "payment_intent.payment_failed" => ParsePaymentFailed(stripeEvent),
                "charge.refunded" => ParseRefunded(stripeEvent),
                _ => (TransactionStatus.Pending, null, string.Empty)
            };

            return new PaymentWebhookEvent(
                Gateway: PaymentGateway.Stripe,
                GatewayEventId: stripeEvent.Id,
                EventType: stripeEvent.Type,
                GatewayTransactionId: gatewayTxId,
                TransactionStatus: status,
                FailureReason: failureReason,
                RawPayload: rawBody
            );
        }

        private static (TransactionStatus, string?, string) ParseSessionCompleted(Event stripeEvent)
        {
            var session = stripeEvent.Data.Object as Session;
            return (TransactionStatus.Success, null, session?.Id ?? string.Empty);
        }

        private static (TransactionStatus, string?, string) ParsePaymentFailed(Event stripeEvent)
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;
            return (TransactionStatus.Failed,
                    intent?.LastPaymentError?.Message ?? "Payment failed",
                    intent?.Id ?? string.Empty);
        }

        private static (TransactionStatus, string?, string) ParseRefunded(Event stripeEvent)
        {
            var charge = stripeEvent.Data.Object as Charge;
            return (TransactionStatus.Refunded, null, charge?.PaymentIntentId ?? string.Empty);
        }
    }

    // ──────────────────────────────────────────────────────────
    // PAYMOB GATEWAY ADAPTER (Egypt / MENA)
    // ──────────────────────────────────────────────────────────

    public class PaymobOptions
    {
        public string ApiKey { get; set; } = default!;
        public string IntegrationId { get; set; } = default!;
        public string IframeId { get; set; } = default!;
        public string HmacSecret { get; set; } = default!;
    }

    public class PaymobGatewayAdapter : IPaymentGatewayAdapter
    {
        // ── Paymob uses a 3-step API: Auth → Order → Payment Key ──
        // Step 1: POST /api/auth/tokens         → auth_token
        // Step 2: POST /api/ecommerce/orders    → order_id
        // Step 3: POST /api/acceptance/payment_keys → payment_token
        // Step 4: Redirect to iframe with payment_token

        private const string BaseUrl = "https://accept.paymob.com";
        private readonly PaymobOptions _options;
        private readonly HttpClient _http;
        private readonly ILogger<PaymobGatewayAdapter> _logger;

        public PaymobGatewayAdapter(
            IOptions<PaymobOptions> options,
            HttpClient http,
            ILogger<PaymobGatewayAdapter> logger)
        {
            _options = options.Value;
            _http = http;
            _logger = logger;
        }

        public async Task<(string PaymentUrl, string GatewayTransactionId)> CreateSessionAsync(
            Guid transactionId, decimal amount, string currency,
            string successUrl, string cancelUrl, CancellationToken ct = default)
        {
            // ── Step 1: Get auth token ─────────────────────────────
            var authRes = await PostJsonAsync<JsonElement>("/api/auth/tokens",
                new { api_key = _options.ApiKey }, ct);
            var authToken = authRes.GetProperty("token").GetString()!;

            // ── Step 2: Create order ───────────────────────────────
            var amountCents = (long)(amount * 100);
            var orderRes = await PostJsonAsync<JsonElement>("/api/ecommerce/orders",
                new
                {
                    auth_token = authToken,
                    delivery_needed = false,
                    amount_cents = amountCents,
                    currency,
                    merchant_order_id = transactionId.ToString(),
                    items = Array.Empty<object>()
                }, ct);
            var orderId = orderRes.GetProperty("id").GetInt64();

            // ── Step 3: Get payment key ────────────────────────────
            var keyRes = await PostJsonAsync<JsonElement>("/api/acceptance/payment_keys",
                new
                {
                    auth_token = authToken,
                    amount_cents = amountCents,
                    expiration = 3600,
                    order_id = orderId,
                    billing_data = new { first_name = "Customer", last_name = ".", email = "na@domain.com", phone_number = "N/A", country = "EG", city = "N/A", street = "N/A", building = "N/A", floor = "N/A", apartment = "N/A" },
                    currency,
                    integration_id = int.Parse(_options.IntegrationId),
                    lock_order_when_paid = false
                }, ct);
            var paymentToken = keyRes.GetProperty("token").GetString()!;

            var iframeUrl = $"{BaseUrl}/api/acceptance/iframes/{_options.IframeId}?payment_token={paymentToken}";

            _logger.LogInformation(
                "Paymob session created. OrderId={OrderId}", orderId);

            return (iframeUrl, orderId.ToString());
        }

        /// <summary>
        /// Paymob sends an HMAC-SHA512 signature in the query string.
        /// rawBody is the JSON callback body from the Paymob transaction webhook.
        /// signatureHeader should contain the "hmac" query param value.
        /// </summary>
        public PaymentWebhookEvent ParseWebhookPayload(string rawBody, string signatureHeader)
        {
            // ── 1. Verify HMAC ─────────────────────────────────────
            var doc = JsonDocument.Parse(rawBody);
            var obj = doc.RootElement.GetProperty("obj");
            var hmacInput = BuildPaymobHmacString(obj);
            var expected = ComputeHmacSha512(hmacInput, _options.HmacSecret);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(signatureHeader)))
                throw new InvalidOperationException("Paymob HMAC verification failed.");

            // ── 2. Parse result ────────────────────────────────────
            var success = obj.GetProperty("success").GetBoolean();
            var gatewayOrderId = obj.GetProperty("order").GetProperty("id").GetInt64().ToString();
            var txId = obj.GetProperty("id").GetInt64().ToString();

            return new PaymentWebhookEvent(
                Gateway: PaymentGateway.Paymob,
                GatewayEventId: txId,
                EventType: success ? "transaction.success" : "transaction.failed",
                GatewayTransactionId: gatewayOrderId,
                TransactionStatus: success ? TransactionStatus.Success : TransactionStatus.Failed,
                FailureReason: success ? null : "Transaction declined by issuer",
                RawPayload: rawBody
            );
        }

        // ── Paymob HMAC: concatenate specific fields in a fixed order ──
        private static string BuildPaymobHmacString(JsonElement obj)
        {
            var fields = new[]
            {
            "amount_cents", "created_at", "currency", "error_occured",
            "has_parent_transaction", "id", "integration_id", "is_3d_secure",
            "is_auth", "is_capture", "is_refunded", "is_standalone_payment",
            "is_voided", "order.id", "owner", "pending",
            "source_data.pan", "source_data.sub_type", "source_data.type",
            "success"
        };

            var sb = new StringBuilder();
            foreach (var field in fields)
            {
                var parts = field.Split('.');
                var val = parts.Length == 1
                    ? obj.GetProperty(parts[0]).ToString()
                    : obj.GetProperty(parts[0]).GetProperty(parts[1]).ToString();
                sb.Append(val);
            }
            return sb.ToString();
        }

        private static string ComputeHmacSha512(string data, string key)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLower();
        }

        private async Task<T> PostJsonAsync<T>(string path, object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync($"{BaseUrl}{path}", content, ct);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(body)!;
        }
    }
}
