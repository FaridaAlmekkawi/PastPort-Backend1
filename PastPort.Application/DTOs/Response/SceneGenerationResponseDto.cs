namespace PastPort.Application.DTOs.Response;

public class Vector3Dto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class SceneObjectDto
{
    public string ObjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TripoPrompt { get; set; } = string.Empty;
    public Vector3Dto Position { get; set; } = new();
    public Vector3Dto Rotation { get; set; } = new();
    public Vector3Dto Scale { get; set; } = new();
    public int Quantity { get; set; }
}

public class NpcObjectDto : SceneObjectDto
{
    public string Role { get; set; } = string.Empty;
}

public class ScenePointDto
{
    public string PointId { get; set; } = string.Empty;
    public string TargetObjectId { get; set; } = string.Empty;
    public string TargetCategory { get; set; } = string.Empty;
    public bool IsLandmark { get; set; }
    public Vector3Dto Position { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Completed { get; set; }
}

public class WorldContextDto
{
    public string Civilization { get; set; } = string.Empty;
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string? RoleOrName { get; set; }
}

public class LightingDto
{
    public string AmbientColor { get; set; } = string.Empty;
    public float AmbientIntensity { get; set; }
    public string DirectionalColor { get; set; } = string.Empty;
    public float DirectionalIntensity { get; set; }
    public float DirectionalAngle { get; set; }
}

public class SkyboxDto
{
    public string Type { get; set; } = string.Empty;
    public string FogColor { get; set; } = string.Empty;
    public float FogDensity { get; set; }
}

public class SceneGenerationResponseDto
{
    public string SceneType { get; set; } = string.Empty;
    public string TimeOfDay { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
    public string Atmosphere { get; set; } = string.Empty;
    public string SelectedYear { get; set; } = string.Empty;
    public WorldContextDto WorldContext { get; set; } = new();
    public List<SceneObjectDto> Structures { get; set; } = new();
    public List<SceneObjectDto> Props { get; set; } = new();
    public List<SceneObjectDto> GroundDetails { get; set; } = new();
    public List<SceneObjectDto> Vegetation { get; set; } = new();
    public List<NpcObjectDto> Npcs { get; set; } = new();
    public LightingDto Lighting { get; set; } = new();
    public SkyboxDto Skybox { get; set; } = new();
    public string HistoricalNotes { get; set; } = string.Empty;
    public List<ScenePointDto> Points { get; set; } = new();
}
