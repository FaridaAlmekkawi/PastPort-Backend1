// PastPort.Domain/Entities/Subscription.cs
using PastPort.Domain.Enums;
namespace PastPort.Domain.Entities;

/// <summary>
/// Represents a user's subscription to a service plan. Tracks the subscription
/// lifecycle from creation through activation, renewal, and cancellation.
/// Linked to <see cref="ApplicationUser"/> and contains a collection of
/// associated <see cref="Payment"/> records.
/// </summary>
public class Subscription
{
    /// <summary>Gets or sets the unique identifier for this subscription.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the ID of the subscribing user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the subscription plan tier.</summary>
    public SubscriptionPlan Plan { get; set; }

    /// <summary>Gets or sets the current status of this subscription (Active, PendingPayment, Cancelled, etc.).</summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>Gets or sets the UTC date when the subscription period started.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Gets or sets the UTC date when the current billing period ends.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Gets or sets the subscription price per billing cycle.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the Stripe subscription ID for gateway operations.</summary>
    public string StripeSubscriptionId { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the subscription auto-renews at period end.</summary>
    public bool AutoRenew { get; set; }

    /// <summary>Gets or sets the navigation property to the subscribing user.</summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Gets or sets the collection of payment records for this subscription.</summary>
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}