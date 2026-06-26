namespace PastPort.Domain.Entities;

public enum VrSessionStatus
{
    Pending,
    Active,
    Completed,
    Disconnected
}

public class VrSession
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Civilization { get; set; } = string.Empty;
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string? RoleOrName { get; set; }
    public DateTime ExpiresAt { get; set; }

    // New fields
    public string UserId { get; set; } = string.Empty;
    public VrSessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
}