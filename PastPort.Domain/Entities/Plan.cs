using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PastPort.Domain.Enums;

namespace PastPort.Domain.Entities
{
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
}
