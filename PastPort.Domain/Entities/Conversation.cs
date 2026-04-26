namespace PastPort.Domain.Entities;

/// <summary>
/// Represents a single exchange in a conversation between a user and an NPC
/// <see cref="Character"/>. Each record captures the user's input message
/// and the AI-generated character response for historical reference.
/// </summary>
public class Conversation
{
    /// <summary>Gets or sets the unique identifier for this conversation exchange.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the ID of the user who initiated this conversation.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the ID of the NPC character involved in this exchange.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Gets or sets the user's input message (text or transcribed audio).</summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>Gets or sets the AI-generated response from the NPC character.</summary>
    public string CharacterResponse { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when this exchange occurred.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties

    /// <summary>Gets or sets the navigation property to the user.</summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Gets or sets the navigation property to the NPC character.</summary>
    public Character Character { get; set; } = null!;
}