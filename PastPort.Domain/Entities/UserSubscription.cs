using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Domain.Entities
{
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

}
