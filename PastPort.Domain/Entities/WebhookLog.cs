using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PastPort.Domain.Enums;

namespace PastPort.Domain.Entities
{
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
}
