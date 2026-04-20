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

}
