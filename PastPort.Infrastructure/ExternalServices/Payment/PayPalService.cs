using Microsoft.EntityFrameworkCore;          // ← ToListAsync
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PastPort.Application.Common;
using PastPort.Application.DTOs;
using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;     // ← PaymentStatus
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Infrastructure.Data;
using Mapster;
using System.Text.Json;


namespace PastPort.Infrastructure.ExternalServices.Payment;
public class PayPalService : IPaymentService
{
    private readonly PayPalSettings _payPalSettings;
    private readonly ILogger<PayPalService> _logger;
    private readonly ApplicationDbContext _db;

    public PayPalService(
        IOptions<PayPalSettings> payPalSettings,
        ILogger<PayPalService> logger,
        ApplicationDbContext db)
    {
        _payPalSettings = payPalSettings.Value;
        _logger = logger;
        _db = db;
    }

    public Task<(string PaymentUrl, string GatewayTransactionId)> CreateCheckoutSessionAsync(
        Guid subscriptionId, Guid transactionId, decimal amount, string currency,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var orderId = Guid.NewGuid().ToString();
        var approvalLink = $"https://sandbox.paypal.com/checkoutnow?token={orderId}";
        return Task.FromResult((approvalLink, orderId));
    }

    public Task<PaymentWebhookEvent> ParseAndVerifyWebhookAsync(
        string rawBody, string signatureHeader, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            throw new InvalidOperationException("Webhook body is empty.");

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;

        var eventId = GetString(root, "id")
                      ?? GetString(root, "event_id")
                      ?? Guid.NewGuid().ToString("N");
        var eventType = GetString(root, "event_type")
                        ?? GetString(root, "type")
                        ?? "payment.unknown";
        var gatewayTransactionId = TryGetGatewayTransactionId(root);

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
            throw new InvalidOperationException("Webhook payload does not include a gateway transaction id.");

        return Task.FromResult(new PaymentWebhookEvent(
            Gateway: PaymentGateway.PayPal,
            GatewayEventId: eventId,
            EventType: eventType,
            GatewayTransactionId: gatewayTransactionId,
            TransactionStatus: MapTransactionStatus(eventType, root),
            FailureReason: GetString(root, "summary"),
            RawPayload: rawBody));
    }

    public async Task ProcessWebhookAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        var existingLog = await _db.WebhookLogs
            .FirstOrDefaultAsync(w =>
                w.Gateway == webhookEvent.Gateway &&
                w.GatewayEventId == webhookEvent.GatewayEventId, ct);

        if (existingLog?.Processed == true)
        {
            _logger.LogInformation("Skipping duplicate webhook {EventId}.", webhookEvent.GatewayEventId);
            return;
        }

        var log = existingLog ?? new WebhookLog
        {
            Gateway = webhookEvent.Gateway,
            GatewayEventId = webhookEvent.GatewayEventId,
            EventType = webhookEvent.EventType,
            Payload = webhookEvent.RawPayload
        };

        if (existingLog is null)
            _db.WebhookLogs.Add(log);

        var transaction = await _db.PaymentTransactions
            .Include(t => t.UserSubscription)
            .Include(t => t.Invoice)
            .FirstOrDefaultAsync(t => t.GatewayTransactionId == webhookEvent.GatewayTransactionId, ct);

        if (transaction is null)
        {
            log.ProcessingError = $"Transaction '{webhookEvent.GatewayTransactionId}' was not found.";
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("No transaction found for PayPal webhook GatewayTxId={GatewayTxId}.", webhookEvent.GatewayTransactionId);
            return;
        }

        switch (webhookEvent.TransactionStatus)
        {
            case TransactionStatus.Success:
                transaction.Status = TransactionStatus.Success;
                transaction.ProcessedAt = DateTime.UtcNow;
                transaction.UserSubscription.Status = SubscriptionStatus.Active;
                transaction.UserSubscription.UpdatedAt = DateTime.UtcNow;
                if (transaction.Invoice is not null)
                {
                    transaction.Invoice.Status = InvoiceStatus.Paid;
                    transaction.Invoice.PaidAt = DateTime.UtcNow;
                }
                break;

            case TransactionStatus.Failed:
                transaction.Status = TransactionStatus.Failed;
                transaction.FailureReason = webhookEvent.FailureReason ?? "Payment failed.";
                transaction.ProcessedAt = DateTime.UtcNow;
                transaction.UserSubscription.Status = transaction.UserSubscription.Status == SubscriptionStatus.Active
                    ? SubscriptionStatus.PastDue
                    : SubscriptionStatus.PendingPayment;
                transaction.UserSubscription.UpdatedAt = DateTime.UtcNow;
                break;

            case TransactionStatus.Refunded:
                transaction.Status = TransactionStatus.Refunded;
                transaction.ProcessedAt = DateTime.UtcNow;
                break;
        }

        log.Processed = true;
        log.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionHistoryAsync(
        string userId, CancellationToken ct = default)
    {
        var txs = await _db.PaymentTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return txs.Select(t => t.Adapt<TransactionDto>());
    }

    public async Task<IEnumerable<InvoiceDto>> GetInvoicesAsync(
        string userId, CancellationToken ct = default)
    {
        var invoices = await _db.Invoices
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync(ct);

        return invoices.Select(i => i.Adapt<InvoiceDto>());
    }

    public Task<PayPalPaymentResponseDto> CreateOrderAsync(
        string userId, PayPalPaymentRequestDto request, decimal amount)
    {
        var orderId = Guid.NewGuid().ToString();
        return Task.FromResult(new PayPalPaymentResponseDto
        {
            Success = true,
            Message = "Order created successfully",
            OrderId = orderId,
            ApprovalLink = $"https://sandbox.paypal.com/checkoutnow?token={orderId}",
            Status = PaymentStatus.Pending
        });
    }

    public Task<PayPalPaymentResponseDto> CaptureOrderAsync(string orderId)
        => Task.FromResult(new PayPalPaymentResponseDto
        {
            Success = true,
            Message = "Payment completed",
            OrderId = orderId,
            Status = PaymentStatus.Completed
        });

    public Task<PayPalPaymentResponseDto> GetOrderDetailsAsync(string orderId)
        => Task.FromResult(new PayPalPaymentResponseDto
        {
            Success = true,
            Message = "Order details retrieved",
            OrderId = orderId,
            Status = PaymentStatus.Completed
        });

    private static string? TryGetGatewayTransactionId(JsonElement root)
    {
        var direct = GetString(root, "gateway_transaction_id")
                     ?? GetString(root, "gatewayTransactionId")
                     ?? GetString(root, "transaction_id");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        if (!root.TryGetProperty("resource", out var resource))
            return GetString(root, "id");

        return GetString(resource, "id")
               ?? GetString(resource, "order_id")
               ?? GetNestedString(resource, "supplementary_data", "related_ids", "order_id")
               ?? GetNestedString(resource, "purchase_units", 0, "payments", "captures", 0, "id");
    }

    private static TransactionStatus MapTransactionStatus(string eventType, JsonElement root)
    {
        var normalized = eventType.ToUpperInvariant();
        var status = GetNestedString(root, "resource", "status")?.ToUpperInvariant() ?? "";

        if (normalized.Contains("REFUND") || status.Contains("REFUND"))
            return TransactionStatus.Refunded;

        if (normalized.Contains("COMPLETED") || normalized.Contains("SUCCESS") ||
            status is "COMPLETED" or "APPROVED" or "CAPTURED")
            return TransactionStatus.Success;

        if (normalized.Contains("FAILED") || normalized.Contains("DENIED") || normalized.Contains("VOIDED") ||
            status is "FAILED" or "DENIED" or "VOIDED" or "DECLINED")
            return TransactionStatus.Failed;

        return TransactionStatus.Pending;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? GetNestedString(JsonElement element, string arrayName, int index, string childName, string childArrayName, int childIndex, string propertyName)
    {
        if (!element.TryGetProperty(arrayName, out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() <= index)
            return null;

        var item = array[index];
        if (!item.TryGetProperty(childName, out var child) ||
            !child.TryGetProperty(childArrayName, out var childArray) ||
            childArray.ValueKind != JsonValueKind.Array ||
            childArray.GetArrayLength() <= childIndex)
            return null;

        return GetString(childArray[childIndex], propertyName);
    }
}
