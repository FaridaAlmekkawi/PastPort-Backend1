using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PastPort.Domain.Enums;

namespace PastPort.Domain.Entities
{
    public sealed class PaymentTransaction
    {
        [Key]
        public Guid Id { get; init; } = Guid.NewGuid();

        public Guid UserSubscriptionId { get; init; }
        public UserSubscription UserSubscription { get; init; } = null!;

        [Required]
        public string UserId { get; init; } = null!;

        /// <summary>Amount in the smallest currency unit (e.g., 999 = $9.99).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; init; }

        [MaxLength(3)]
        public string Currency { get; init; } = "USD";

        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public ApplicationUser? User { get; init; }
        public PaymentGateway Gateway { get; init; }

        /// <summary>Gateway-assigned charge/payment-intent ID.</summary>
        [MaxLength(300)]
        public string? GatewayTransactionId { get; set; }

        /// <summary>Raw gateway response payload (for debugging & audit).</summary>
        public string? GatewayResponse { get; init; }

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
        public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();

        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        // ── Navigation ──────────────────────────────────────────
        public Invoice? Invoice { get; init; }
    }
}
