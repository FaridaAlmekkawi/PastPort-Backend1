using PastPort.Domain.Enums;

namespace PastPort.Domain.Entities;

public class UserExperience
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public Guid? SceneId { get; set; }
    public HistoricalScene? Scene { get; set; }

    public string Civilization { get; set; } = string.Empty;
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string? RoleOrName { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string? VrSessionId { get; set; }

    public ExperienceStatus Status { get; set; } = ExperienceStatus.Started;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
