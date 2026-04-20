using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace PastPort.Domain.Entities
{
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
}
