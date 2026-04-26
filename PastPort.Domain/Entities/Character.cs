namespace PastPort.Domain.Entities;

/// <summary>
/// Represents an AI-powered NPC character within a <see cref="HistoricalScene"/>.
/// Characters have distinct personalities, backgrounds, and voice identities
/// that the AI service uses to generate historically contextualized responses.
/// </summary>
public class Character
{
    /// <summary>Gets or sets the unique identifier for this character.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the character's display name (e.g., "Cleopatra VII").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's historical role (e.g., "Pharaoh of Egypt").</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's historical background, used as AI context.</summary>
    public string Background { get; set; } = string.Empty;

    /// <summary>Gets or sets personality traits that influence the AI's conversational tone.</summary>
    public string Personality { get; set; } = string.Empty;

    /// <summary>Gets or sets the voice synthesis identifier used by the AI audio pipeline.</summary>
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL to the character's avatar image for UI display.</summary>
    public string AvatarUrl { get; set; } = string.Empty;

    // Foreign Key

    /// <summary>Gets or sets the ID of the <see cref="HistoricalScene"/> this character belongs to.</summary>
    public Guid SceneId { get; set; }

    /// <summary>Gets or sets the navigation property to the parent scene.</summary>
    public HistoricalScene Scene { get; set; } = null!;

    /// <summary>Gets or sets the UTC timestamp when this character was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}