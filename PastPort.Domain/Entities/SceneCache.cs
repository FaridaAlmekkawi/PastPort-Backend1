namespace PastPort.Domain.Entities;

public class SceneCache
{
    public Guid Id { get; set; }
    public string CacheKey { get; set; } = string.Empty; // SHA256 for civilization/location/goal/role
    public string Civilization { get; set; } = string.Empty;
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string Goal { get; set; } = "Educational";
    public string? RoleOrName { get; set; }
    public string SceneJson { get; set; } = string.Empty; // الـ JSON كاملاً
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } // بعد كام يوم تعتبره قديم
}
