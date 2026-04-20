using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Domain.Entities
{
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
}
