namespace PastPort.Domain.Entities;

/// <summary>
/// Represents a reconstructed historical scene that users can explore in VR.
/// Each scene is defined by a time period, location, and environment prompt
/// that drives the AI context for NPC conversations within this scene.
/// </summary>
public class HistoricalScene
{
    /// <summary>Gets or sets the unique identifier for this scene.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display title (e.g., "Ancient Alexandria").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the historical era or time period (e.g., "300 BC").</summary>
    public string Era { get; set; } = string.Empty;

    /// <summary>Gets or sets the geographical location (e.g., "Alexandria, Egypt").</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Gets or sets a human-readable description of the scene.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment prompt sent to the AI/LLM to establish
    /// the historical and atmospheric context for NPC conversations.
    /// </summary>
    public string EnvironmentPrompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL to the 3D model file used by Unity to render this scene.</summary>
    public string Model3DUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when this scene was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the last update, if any.</summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties

    /// <summary>Gets or sets the NPC characters that inhabit this historical scene.</summary>
    public ICollection<Character> Characters { get; set; } = new List<Character>();
}