// PastPort.Domain/Entities/Payment.cs
using PastPort.Domain.Enums;
namespace PastPort.Domain.Entities;
public class Payment
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PayPalOrderId { get; set; } = string.Empty;
    public string PayerEmail { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser User { get; set; } = null!;
    public Guid? SubscriptionId { get; set; }
}