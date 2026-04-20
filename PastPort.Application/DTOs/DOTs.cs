using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PastPort.Domain.Enums;
namespace PastPort.Application.DTOs
{
    // ──────────────────────────────────────────────────────────
    // PLAN DTOs
    // ──────────────────────────────────────────────────────────

    public record PlanDto(
        Guid Id,
        string Name,
        string? Description,
        decimal Price,
        string Currency,
        BillingCycle BillingCycle,
        int TrialDays,
        int DisplayOrder,
        List<FeatureDto> Features
    );

    public record FeatureDto(
        Guid Id,
        string Name,
        string Slug,
        string? Description,
        int? Limit,
        bool IsEnabled
    );

    // ──────────────────────────────────────────────────────────
    // SUBSCRIPTION DTOs
    // ──────────────────────────────────────────────────────────

    public record UserSubscriptionDto(
    Guid Id,
        PlanDto Plan,
        SubscriptionStatus Status,
        DateTime CurrentPeriodStart,
        DateTime CurrentPeriodEnd,
        DateTime? TrialEnd,
        bool AutoRenew,
        DateTime? CancelledAt
    );

    public record InitiateCheckoutRequest(
        Guid PlanId,
        PaymentGateway Gateway,
        string SuccessUrl,   // Where to redirect after successful payment
        string CancelUrl    // Where to redirect on cancellation
    );

    public record InitiateCheckoutResponse(
        Guid TransactionId,
        Guid SubscriptionId,
        string PaymentUrl,   // Redirect the frontend to this URL
        string Status
    );

    public record UpgradePlanRequest(
        Guid NewPlanId,
        bool ApplyProration = true
    );

    // ──────────────────────────────────────────────────────────
    // TRANSACTION & INVOICE DTOs
    // ──────────────────────────────────────────────────────────

    public record TransactionDto(
        Guid Id,
        decimal Amount,
        string Currency,
        TransactionStatus Status,
        PaymentGateway Gateway,
        string? GatewayTransactionId,
        string? FailureReason,
        DateTime CreatedAt,
        DateTime? ProcessedAt
    );

    public record InvoiceDto(
        Guid Id,
        string InvoiceNumber,
        decimal SubTotal,
        decimal TaxAmount,
        decimal DiscountAmount,
        decimal TotalAmount,
        string Currency,
        InvoiceStatus Status,
        DateTime IssuedAt,
        DateTime? PaidAt,
        string? PdfUrl
    );

    // ──────────────────────────────────────────────────────────
    // WEBHOOK PAYLOAD — Normalized internal event (gateway-agnostic)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Normalized payment event extracted from any gateway's webhook payload.
    /// The gateway adapters (Stripe, Paymob, etc.) parse raw JSON into this shape.
    /// </summary>
    public record PaymentWebhookEvent(
        PaymentGateway Gateway,
        string GatewayEventId,
        string EventType,                   // "payment.success" | "payment.failed" | "subscription.cancelled"
        string GatewayTransactionId,
        TransactionStatus TransactionStatus,
        string? FailureReason,
        string RawPayload
    );
}
