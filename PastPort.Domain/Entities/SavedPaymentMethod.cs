// PastPort.Domain/Entities/SavedPaymentMethod.cs
namespace PastPort.Domain.Entities;
public class SavedPaymentMethod
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ProviderPaymentMethodId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser User { get; set; } = null!;
}