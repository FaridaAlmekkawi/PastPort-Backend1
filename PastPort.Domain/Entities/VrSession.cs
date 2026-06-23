namespace PastPort.Domain.Entities;

public class VrSession
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Civilization { get; set; } = string.Empty;
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string? RoleOrName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}