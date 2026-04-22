using Microsoft.EntityFrameworkCore;          // ← ToListAsync
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PastPort.Application.Common;
using PastPort.Application.DTOs;
using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;     // ← PaymentStatus
using PastPort.Infrastructure.Data;


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
        => throw new NotImplementedException("PayPal webhook parsing not implemented.");

    public Task ProcessWebhookAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
        => throw new NotImplementedException("PayPal webhook processing not implemented.");

    public async Task<IEnumerable<TransactionDto>> GetTransactionHistoryAsync(
        string userId, CancellationToken ct = default)
    {
        var txs = await _db.PaymentTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return txs.Select(t => new TransactionDto(
            t.Id, t.Amount, t.Currency,
            t.Status, t.Gateway,
            t.GatewayTransactionId, t.FailureReason,
            t.CreatedAt, t.ProcessedAt));
    }

    public async Task<IEnumerable<InvoiceDto>> GetInvoicesAsync(
        string userId, CancellationToken ct = default)
    {
        var invoices = await _db.Invoices
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync(ct);

        return invoices.Select(i => new InvoiceDto(
            i.Id, i.InvoiceNumber,
            i.SubTotal, i.TaxAmount, i.DiscountAmount, i.TotalAmount,
            i.Currency, i.Status,
            i.IssuedAt, i.PaidAt, i.PdfUrl));
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
}