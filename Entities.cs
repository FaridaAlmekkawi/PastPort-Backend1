// ============================================================
//  Entities.cs — EF Core Entity Models
//  Subscription Plans, User Subscriptions, Features,
//  Transactions, Invoices, and Webhook Log
// ============================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SubscriptionPayment.Domain.Enums;

namespace SubscriptionPayment.Domain.Entities;

// ──────────────────────────────────────────────────────────
// 1. PLAN — The product the user buys (e.g., "Explorer Pro")
// ──────────────────────────────────────────────────────────
public class Plan
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = default!;           // e.g., "Explorer Pro"

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Price per billing cycle in smallest currency unit (e.g., cents).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";         // ISO 4217

    public BillingCycle BillingCycle { get; set; }

    /// <summary>Trial days granted upon first subscription. 0 = no trial.</summary>
    public int TrialDays { get; set; } = 0;

    /// <summary>Sort order for display on the pricing page.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;

    /// <summary>Gateway-specific product/price IDs (serialized JSON).</summary>
    public string? GatewayMetadata { get; set; }          // { "stripe_price_id": "price_xxx" }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ──────────────────────────────────────────
    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}

// ──────────────────────────────────────────────────────────
// 2. FEATURE — A capability gated behind a plan
//    (e.g., "Explore Secrets", "Unlimited Scenarios")
// ──────────────────────────────────────────────────────────
public class Feature
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = default!;          // e.g., "UnlimitedScenarios"

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string Slug { get; set; } = default!;          // machine-readable key

    public bool IsActive { get; set; } = true;

    // ── Navigation ──────────────────────────────────────────
    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
}

// ──────────────────────────────────────────────────────────
// 3. PLAN FEATURE — Many-to-many join: which features a plan includes
// ──────────────────────────────────────────────────────────
public class PlanFeature
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = default!;

    public Guid FeatureId { get; set; }
    public Feature Feature { get; set; } = default!;

    /// <summary>
    /// Optional limit for metered features (null = unlimited).
    /// E.g., "MaxScenarios = 5" on a Starter plan.
    /// </summary>
    public int? Limit { get; set; }

    public bool IsEnabled { get; set; } = true;
}

// ──────────────────────────────────────────────────────────
// 4. USER SUBSCRIPTION — A user's active (or historical) subscription
// ──────────────────────────────────────────────────────────
public class UserSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = default!;        // FK to ASP.NET Identity User

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = default!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.PendingPayment;

    /// <summary>Start of the current paid billing period.</summary>
    public DateTime CurrentPeriodStart { get; set; }

    /// <summary>End of the current paid billing period. Access revoked after this.</summary>
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>If on trial, when the trial ends.</summary>
    public DateTime? TrialEnd { get; set; }

    /// <summary>
    /// For upgrades: price difference credited back (prorated amount in cents).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ProrationCredit { get; set; }

    /// <summary>Whether auto-renewal is enabled.</summary>
    public bool AutoRenew { get; set; } = true;

    /// <summary>When the user explicitly cancelled (for grace-period logic).</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Previous plan ID (populated on upgrade/downgrade for audit trail).
    /// </summary>
    public Guid? PreviousPlanId { get; set; }

    /// <summary>Gateway subscription ID (e.g., Stripe sub_xxx).</summary>
    [MaxLength(200)]
    public string? GatewaySubscriptionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ──────────────────────────────────────────
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

// ──────────────────────────────────────────────────────────
// 5. PAYMENT TRANSACTION — Every attempt to charge the user
// ──────────────────────────────────────────────────────────
public class PaymentTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserSubscriptionId { get; set; }
    public UserSubscription UserSubscription { get; set; } = default!;

    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>Amount in smallest currency unit (e.g., 999 = $9.99).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public PaymentGateway Gateway { get; set; }

    /// <summary>Gateway-assigned charge/payment-intent ID.</summary>
    [MaxLength(300)]
    public string? GatewayTransactionId { get; set; }

    /// <summary>Raw gateway response payload (for debugging & audit).</summary>
    public string? GatewayResponse { get; set; }

    /// <summary>Redirect URL the gateway sends the user to on success.</summary>
    [MaxLength(1000)]
    public string? GatewayPaymentUrl { get; set; }

    /// <summary>Human-readable failure reason returned by the gateway.</summary>
    [MaxLength(500)]
    public string? FailureReason { get; set; }

    /// <summary>
    /// Idempotency key sent to the gateway to prevent duplicate charges.
    /// </summary>
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    // ── Navigation ──────────────────────────────────────────
    public Invoice? Invoice { get; set; }
}

// ──────────────────────────────────────────────────────────
// 6. INVOICE — PDF-able billing record issued per transaction
// ──────────────────────────────────────────────────────────
public class Invoice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-friendly invoice number: INV-2024-000001</summary>
    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = default!;

    public Guid UserSubscriptionId { get; set; }
    public UserSubscription UserSubscription { get; set; } = default!;

    public Guid? PaymentTransactionId { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }

    [Required]
    public string UserId { get; set; } = default!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? DueDate { get; set; }

    /// <summary>Billing address snapshot at time of purchase.</summary>
    public string? BillingAddressJson { get; set; }

    /// <summary>Path or URL to generated PDF.</summary>
    [MaxLength(500)]
    public string? PdfUrl { get; set; }
}

// ──────────────────────────────────────────────────────────
// 7. WEBHOOK LOG — Idempotent log of every incoming webhook event
// ──────────────────────────────────────────────────────────
public class WebhookLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public PaymentGateway Gateway { get; set; }

    /// <summary>Gateway's event ID (used for idempotency checks).</summary>
    [Required, MaxLength(300)]
    public string GatewayEventId { get; set; } = default!;

    [MaxLength(100)]
    public string EventType { get; set; } = default!;     // e.g., "payment_intent.succeeded"

    /// <summary>Raw JSON body received from the gateway.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>Whether our handler processed this event successfully.</summary>
    public bool Processed { get; set; } = false;

    [MaxLength(500)]
    public string? ProcessingError { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}