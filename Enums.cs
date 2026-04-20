// ============================================================
//  Enums.cs — Domain Enumerations
//  All status codes used across Subscription & Payment domain
// ============================================================
namespace SubscriptionPayment.Domain.Enums;

/// <summary>Lifecycle state of a user's subscription.</summary>
public enum SubscriptionStatus
{
    PendingPayment = 0,   // Created but payment not yet confirmed
    Active = 1,   // Payment confirmed, access granted
    PastDue = 2,   // Renewal payment failed
    Cancelled = 3,   // Voluntarily cancelled; may still have access until PeriodEnd
    Expired = 4,   // PeriodEnd passed and not renewed
    Trialing = 5,   // Free trial period
    Paused = 6    // Admin/user paused
}

/// <summary>Lifecycle state of a single payment attempt.</summary>
public enum TransactionStatus
{
    Pending = 0,   // Initiated, waiting for gateway response
    Success = 1,   // Gateway confirmed payment
    Failed = 2,   // Gateway rejected payment
    Refunded = 3,   // Full refund issued
    Disputed = 4,   // Chargeback / dispute opened
    Cancelled = 5    // Cancelled before gateway processed
}

/// <summary>How often the subscription billing cycle repeats.</summary>
public enum BillingCycle
{
    Monthly = 1,
    Yearly = 2,
    Weekly = 3,
    OneTime = 4    // Lifetime / one-time purchase
}

/// <summary>Supported payment gateways.</summary>
public enum PaymentGateway
{
    Stripe = 1,
    PayPal = 2,
    Paymob = 3,   // Popular in MENA/Egypt
    Manual = 99
}

/// <summary>Type of invoice line item.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Void = 3,
    Overdue = 4
}