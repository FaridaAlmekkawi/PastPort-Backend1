using PastPort.Domain.Enums;

namespace PastPort.Domain.Entities;

/// <summary>
/// Represents a 3D asset (model, texture, audio clip, etc.) that Unity clients
/// download and render within a <see cref="HistoricalScene"/>.
/// Each asset is integrity-verified using a SHA-256 file hash and supports
/// semantic versioning for Unity's incremental sync pipeline.
/// </summary>
public class Asset
{
    /// <summary>Gets or sets the unique identifier for this asset.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display name of the asset (e.g., "Egyptian Pillar").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the unique filename on disk (generated during upload).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the asset type (Model, Texture, Audio, etc.).</summary>
    public PastPort.Domain.Enums.AssetType Type { get; set; }

    /// <summary>Gets or sets the server-side file path for storage operations.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the public URL used by clients to download the asset.</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the file size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the file contents.
    /// Used by Unity clients to verify local cache integrity
    /// via the <c>/api/unityassets/verify</c> endpoint.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the semantic version string (e.g., "1.0.0").</summary>
    public string Version { get; set; } = "1.0.0";

    // Scene Relationship

    /// <summary>Gets or sets the optional scene ID this asset is associated with.</summary>
    public Guid? SceneId { get; set; }

    /// <summary>Gets or sets the navigation property to the parent scene, if any.</summary>
    public HistoricalScene? Scene { get; set; }

    // Metadata

    /// <summary>Gets or sets an optional description of the asset.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets comma-separated tags for categorization.</summary>
    public string? Tags { get; set; }

    /// <summary>Gets or sets the current publication status of the asset.</summary>
    public PastPort.Domain.Enums.AssetStatus Status { get; set; }

    // Unity-Specific

    /// <summary>Gets or sets the asset path within the Unity project hierarchy.</summary>
    public string? UnityAssetPath { get; set; }

    /// <summary>Gets or sets the AssetBundle name for Unity's bundle system.</summary>
    public string? AssetBundleName { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this asset was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the last update, if any.</summary>
    public DateTime? UpdatedAt { get; set; }
}