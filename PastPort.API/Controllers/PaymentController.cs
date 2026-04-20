using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.DTOs;
using PastPort.Application.Interfaces;
using System.Security.Claims;

namespace PastPort.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User identity not found.");

        // ── GET /api/payments/transactions ───────────────────────
        /// <summary>Returns the authenticated user's full payment history.</summary>
        [HttpGet("transactions")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<TransactionDto>), 200)]
        public async Task<IActionResult> GetTransactions(CancellationToken ct)
        {
            var txs = await _paymentService.GetTransactionHistoryAsync(UserId, ct);
            return Ok(txs);
        }

        // ── GET /api/payments/invoices ────────────────────────────
        /// <summary>Returns all invoices for the authenticated user.</summary>
        [HttpGet("invoices")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<InvoiceDto>), 200)]
        public async Task<IActionResult> GetInvoices(CancellationToken ct)
        {
            var invoices = await _paymentService.GetInvoicesAsync(UserId, ct);
            return Ok(invoices);
        }

        // ── POST /api/payments/webhooks/stripe ───────────────────
        /// <summary>
        /// Stripe webhook endpoint.
        ///
        /// SECURITY:
        ///   - [AllowAnonymous] because Stripe cannot authenticate as a user.
        ///   - Signature is validated inside ParseAndVerifyWebhookAsync using
        ///     the STRIPE_WEBHOOK_SECRET. Invalid signatures → 400.
        ///   - Always return 200 quickly; Stripe retries on non-2xx for 3 days.
        ///
        /// SETUP in Stripe Dashboard:
        ///   Events to listen for:
        ///     checkout.session.completed
        ///     payment_intent.payment_failed
        ///     charge.refunded
        ///   Endpoint URL: https://your-domain.com/api/payments/webhooks/stripe
        /// </summary>
        [HttpPost("webhooks/stripe")]
        [AllowAnonymous]
        [Consumes("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> StripeWebhook(CancellationToken ct)
        {
            // Read raw body — must be raw string for signature verification
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var sigHeader = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

            try
            {
                var webhookEvent = await _paymentService.ParseAndVerifyWebhookAsync(rawBody, sigHeader, ct);
                await _paymentService.ProcessWebhookAsync(webhookEvent, ct);
                return Ok();
            }
            catch (Stripe.StripeException ex)
            {
                _logger.LogWarning("Stripe signature verification failed: {Message}", ex.Message);
                return BadRequest(new { error = "Invalid signature" });
            }
            catch (Exception ex)
            {
                // Return 500 so Stripe retries this webhook
                _logger.LogError(ex, "Stripe webhook processing error");
                return StatusCode(500);
            }
        }

        // ── POST /api/payments/webhooks/paymob ───────────────────
        /// <summary>
        /// Paymob transaction callback endpoint.
        ///
        /// SECURITY:
        ///   - Paymob sends HMAC-SHA512 as a query string parameter "hmac".
        ///   - The adapter verifies it; invalid HMAC → 400.
        ///
        /// SETUP in Paymob Dashboard:
        ///   Transaction Processed Callback: https://your-domain.com/api/payments/webhooks/paymob?hmac={hmac}
        /// </summary>
        [HttpPost("webhooks/paymob")]
        [AllowAnonymous]
        [Consumes("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> PaymobWebhook(
            [FromQuery] string hmac,
            CancellationToken ct)
        {
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);

            try
            {
                var webhookEvent = await _paymentService.ParseAndVerifyWebhookAsync(rawBody, hmac, ct);
                await _paymentService.ProcessWebhookAsync(webhookEvent, ct);
                return Ok();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("HMAC"))
            {
                _logger.LogWarning("Paymob HMAC verification failed: {Message}", ex.Message);
                return BadRequest(new { error = "Invalid HMAC" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paymob webhook processing error");
                return StatusCode(500);
            }
        }
    }
}
