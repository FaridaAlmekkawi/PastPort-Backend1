using PastPort.Domain.Entities;

public class Asset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public PastPort.Domain.Enums.AssetType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";

    // ✅ جديد: الـ hash بتاع الـ tripo_prompt نفسه (مش محتوى الملف)
    // ده اللي هنستخدمه عشان نعرف لو نفس الـ prompt اتعمله asset قبل كده
    public string? SourcePromptHash { get; set; }

    public Guid? SceneId { get; set; }
    public HistoricalScene? Scene { get; set; }

    public string? Description { get; set; }
    public string? Tags { get; set; }
    public PastPort.Domain.Enums.AssetStatus Status { get; set; }

    public string? UnityAssetPath { get; set; }
    public string? AssetBundleName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}