using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Domain.Entities
{
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

}
