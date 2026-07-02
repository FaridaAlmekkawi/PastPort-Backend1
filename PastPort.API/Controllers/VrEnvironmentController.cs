using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using PastPort.Application.DTOs.Response;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PastPort.Infrastructure.Data;
namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VrEnvironmentController : ControllerBase
{
    private readonly IVrEnvironmentService _vrService;
    private readonly IAssetRepository _assetRepository;
    private readonly ISceneCacheRepository _sceneCacheRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VrEnvironmentController> _logger;

    // كام يوم الـ scene cache يفضل صالح
    private const int SceneCacheDays = 7;

    public VrEnvironmentController(
        IVrEnvironmentService vrService,
        IAssetRepository assetRepository,
        ISceneCacheRepository sceneCacheRepository,
        IFileStorageService fileStorageService,
        ApplicationDbContext context,
        ILogger<VrEnvironmentController> logger)
    {
        _vrService = vrService;
        _assetRepository = assetRepository;
        _sceneCacheRepository = sceneCacheRepository;
        _fileStorageService = fileStorageService;
        _context = context;
        _logger = logger;
    }

    // ============================================================
    // Unity بيستدعي ده بالـ sessionId — بيجيب الـ scene
    // ============================================================
    [HttpGet("scene/{sessionId}")]
    public async Task<IActionResult> GetScene(string sessionId)
    {
        var userId = CurrentUserId();

        // 1) جيب الـ session من الـ DB
        var session = await _context.VrSessions
            .FirstOrDefaultAsync(s =>
                s.SessionId == sessionId &&
                s.UserId == userId &&
                s.ExpiresAt > DateTime.UtcNow);

        if (session == null)
            return NotFound(new { error = "Session not found or expired" });
        var experience = await _context.UserExperiences
            .FirstOrDefaultAsync(ue => ue.VrSessionId == session.SessionId);
        var goal = MapGoal(!string.IsNullOrWhiteSpace(session.Goal) ? session.Goal : experience?.Goal);
        if (session.Goal != goal)
        {
            session.Goal = goal;
            _context.Entry(session).Property(s => s.Goal).IsModified = true;
            await _context.SaveChangesAsync();
        }

        if (experience?.SceneId is Guid manualSceneId)
        {
            var manualScene = await BuildManualSceneAsync(manualSceneId, session, goal);
            if (manualScene == null)
                return NotFound(new { error = "Manual scene has no available assets" });

            return Ok(new
            {
                success = true,
                cached = false,
                data = manualScene
            });
        }

        var cacheKey = ComputeCacheKey(
            session.Civilization,
            session.LocationOldName,
            goal,
            session.RoleOrName);

        // 3) دوّر في الـ cache الأول
        var cached = await _sceneCacheRepository.GetByCacheKeyAsync(cacheKey);
        if (cached != null)
        {
            _logger.LogInformation("Scene cache HIT: {Key}", cacheKey);

            var cachedScene = JsonSerializer.Deserialize<object>(cached.SceneJson);
            return Ok(new
            {
                success = true,
                cached = true,
                data = cachedScene
            });
        }

        // 4) مش موجود → اطلب من السيرفر الخارجي (~6 دقايق)
        _logger.LogInformation(
            "Scene cache MISS, generating: {Civ}",
            session.Civilization);

        try
        {
            var scene = await _vrService.GenerateSceneAsync(
                session.Civilization,
                session.LocationOldName,
                goal,
                session.RoleOrName);

            // Update YearRange dynamically from AI response
            session.YearRange = scene.WorldContext.YearRange;
            _context.Entry(session).Property(s => s.YearRange).IsModified = true;

            if (experience != null)
            {
                experience.YearRange = scene.WorldContext.YearRange;
                _context.Entry(experience).Property(ue => ue.YearRange).IsModified = true;
            }
            await _context.SaveChangesAsync();

            // 5) خزّن في الـ cache
            var sceneJson = JsonSerializer.Serialize(scene);
            var newCache = new SceneCache
            {
                Id = Guid.NewGuid(),
                CacheKey = cacheKey,
                Civilization = session.Civilization,
                YearRange = session.YearRange,
                LocationOldName = session.LocationOldName,
                Goal = goal,
                RoleOrName = session.RoleOrName,
                SceneJson = sceneJson,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(SceneCacheDays)
            };

            await _sceneCacheRepository.AddAsync(newCache);

            return Ok(new
            {
                success = true,
                cached = false,
                data = scene
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scene generation failed");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // ============================================================
    // Unity بيستدعي ده لكل object في الـ scene
    // ============================================================
    [HttpGet("asset")]
    public async Task<IActionResult> GetAsset(
        [FromQuery] string prompt,
        [FromQuery] bool isNpc = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { error = "Prompt is required" });

        var promptHash = ComputeHash(prompt);

        // 1) دوّر في الـ cache
        var cached = await _assetRepository.GetAssetByPromptHashAsync(promptHash);
        if (cached != null)
        {
            _logger.LogInformation("Asset cache HIT: {Hash}", promptHash);
            return Ok(new
            {
                success = true,
                cached = true,
                data = new
                {
                    fileUrl = cached.FileUrl,
                    assetId = cached.Id,
                    fileName = cached.FileName
                }
            });
        }

        // 2) مش موجود → استدعي السيرفر الخارجي
        _logger.LogInformation("Asset cache MISS, generating: isNpc={IsNpc}", isNpc);

        Stream glbStream;
        try
        {
            glbStream = await _vrService.GenerateAssetAsync(
                prompt, isNpc, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Asset generation failed");
            return StatusCode(502, new { error = ex.Message });
        }

        // 3) خزّن الملف
        using var ms = new MemoryStream();
        await glbStream.CopyToAsync(ms, cancellationToken);
        await glbStream.DisposeAsync();
        var fileBytes = ms.ToArray();

        var fileName = $"{promptHash}.glb";
        var fileUrl = await SaveGlbAsync(fileBytes, fileName);

        // 4) خزّن Record في الـ DB
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            Name = prompt.Length > 100 ? prompt[..100] : prompt,
            FileName = fileName,
            Type = isNpc ? AssetType.Prefab : AssetType.Model3D,
            FilePath = fileUrl,
            FileUrl = fileUrl,
            FileSize = fileBytes.Length,
            FileHash = ComputeHash(fileBytes),
            SourcePromptHash = promptHash,
            Version = "1.0.0",
            Status = AssetStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        await _assetRepository.AddAsync(asset);

        _logger.LogInformation(
            "Asset generated and cached: {FileName} ({Size} bytes)",
            fileName, fileBytes.Length);

        return Ok(new
        {
            success = true,
            cached = false,
            data = new
            {
                fileUrl = asset.FileUrl,
                assetId = asset.Id,
                fileName = asset.FileName
            }
        });
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var healthy = await _vrService.CheckHealthAsync();
        return Ok(new { healthy });
    }

    [HttpGet("current-session")]
    public async Task<IActionResult> GetCurrentSession()
    {
        var userId = CurrentUserId();

        
        // 1) Find the latest session that is Pending
        var session = await _context.VrSessions
            .Where(s => s.UserId == userId && s.Status == VrSessionStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        // 2) If not found, find the latest session that is Active or Disconnected
        if (session == null)
        {
            session = await _context.VrSessions
                .Where(s => s.UserId == userId && (s.Status == VrSessionStatus.Active || s.Status == VrSessionStatus.Disconnected))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (session == null)
            return NotFound(new { success = false, message = "No current session found." });

        return Ok(new { success = true, data = session });
    }

    [HttpPost("session/{id:guid}/start")]
    public async Task<IActionResult> StartSessionById(Guid id)
    {
        var userId = CurrentUserId();

        var session = await _context.VrSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session == null)
            return NotFound(new { success = false, message = "Session not found." });

        if (session.Status == VrSessionStatus.Pending)
        {
            session.Status = VrSessionStatus.Active;
            session.StartedAt = DateTime.UtcNow;
            session.LastHeartbeat = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, data = session });
    }

    [HttpPost("session/{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id)
    {
        var userId = CurrentUserId();

        var session = await _context.VrSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session == null)
            return NotFound(new { success = false, message = "Session not found." });

        session.LastHeartbeat = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpPost("session/{id:guid}/end")]
    public async Task<IActionResult> EndSession(Guid id)
    {
        var userId = CurrentUserId();

        var session = await _context.VrSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session == null)
            return NotFound(new { success = false, message = "Session not found." });

        session.Status = VrSessionStatus.Completed;
        session.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, data = session });
    }

    // ==================== Private Helpers ====================

    private async Task<SceneGenerationResponseDto?> BuildManualSceneAsync(
        Guid sceneId,
        VrSession session,
        string goal)
    {
        var assets = await _assetRepository.GetAssetsBySceneIdAsync(sceneId);
        if (assets.Count == 0)
            return null;

        var historicalScene = await _context.HistoricalScenes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sceneId);

        var scene = new SceneGenerationResponseDto
        {
            Source = "manual_fallback_layout",
            SceneType = "manual_asset_scene",
            TimeOfDay = "morning",
            Weather = "clear_sky",
            Atmosphere = "historical immersive",
            SelectedYear = string.IsNullOrWhiteSpace(session.YearRange) ? string.Empty : session.YearRange,
            WorldContext = new WorldContextDto
            {
                Civilization = session.Civilization,
                YearRange = session.YearRange,
                LocationOldName = session.LocationOldName,
                RoleOrName = session.RoleOrName
            },
            Lighting = new LightingDto
            {
                AmbientColor = "#FFFFFF",
                AmbientIntensity = 0.65f,
                DirectionalColor = "#FFF4CC",
                DirectionalIntensity = 1.0f,
                DirectionalAngle = 45f
            },
            Skybox = new SkyboxDto
            {
                Type = "desert",
                FogColor = "#EDEDED",
                FogDensity = 0.01f
            },
            HistoricalNotes = historicalScene?.Description
                ?? $"Manual scene for {session.Civilization} in {session.LocationOldName}."
        };

        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            var category = ClassifyManualAsset(asset);
            counters.TryGetValue(category, out var index);
            counters[category] = index + 1;

            if (category == "npcs")
            {
                scene.Npcs.Add(new NpcObjectDto
                {
                    ObjectId = asset.Id.ToString(),
                    AssetId = asset.Id.ToString(),
                    Name = asset.Name,
                    FileName = asset.FileName,
                    FileUrl = asset.FileUrl,
                    Role = asset.Name,
                    TripoPrompt = asset.Name,
                    Position = GetManualPosition(category, index),
                    Rotation = GetManualRotation(category, index),
                    Scale = GetManualScale(asset),
                    Quantity = 1
                });
                continue;
            }

            var sceneObject = new SceneObjectDto
            {
                ObjectId = asset.Id.ToString(),
                AssetId = asset.Id.ToString(),
                Name = asset.Name,
                FileName = asset.FileName,
                FileUrl = asset.FileUrl,
                TripoPrompt = asset.Name,
                Position = GetManualPosition(category, index),
                Rotation = GetManualRotation(category, index),
                Scale = GetManualScale(asset),
                Quantity = 1
            };

            switch (category)
            {
                case "structures":
                    scene.Structures.Add(sceneObject);
                    break;
                case "ground_details":
                    scene.GroundDetails.Add(sceneObject);
                    break;
                case "vegetation":
                    scene.Vegetation.Add(sceneObject);
                    break;
                default:
                    scene.Props.Add(sceneObject);
                    break;
            }
        }

        var aiLayoutApplied = await TryApplyAiLayoutAsync(scene, session, goal);
        if (aiLayoutApplied)
            scene.Source = "manual_ai_layout";

        AddManualPoints(scene, goal);
        return scene;
    }

    private async Task<bool> TryApplyAiLayoutAsync(
        SceneGenerationResponseDto scene,
        VrSession session,
        string goal)
    {
        var layoutAssets = EnumerateSceneObjects(scene)
            .Select(item => new ManualSceneLayoutAssetDto
            {
                AssetId = item.Object.AssetId ?? item.Object.ObjectId,
                Name = item.Object.Name,
                Category = item.Category,
                FileName = item.Object.FileName,
                FileUrl = item.Object.FileUrl
            })
            .ToList();

        if (layoutAssets.Count == 0)
            return false;

        var request = new ManualSceneLayoutRequestDto
        {
            Civilization = session.Civilization,
            YearRange = session.YearRange,
            LocationOldName = session.LocationOldName,
            Goal = goal,
            RoleOrName = session.RoleOrName,
            Assets = layoutAssets
        };

        ManualSceneLayoutResponseDto layout;
        try
        {
            layout = await _vrService.GenerateManualLayoutAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual AI layout failed; using deterministic fallback layout.");
            return false;
        }

        var objectsByAssetId = EnumerateSceneObjects(scene)
            .GroupBy(item => item.Object.AssetId ?? item.Object.ObjectId)
            .ToDictionary(group => group.Key, group => group.First().Object);

        var applied = 0;
        foreach (var item in layout.Items)
        {
            if (string.IsNullOrWhiteSpace(item.AssetId) ||
                !objectsByAssetId.TryGetValue(item.AssetId, out var sceneObject))
            {
                continue;
            }

            if (item.Position != null)
                sceneObject.Position = item.Position;
            if (item.Rotation != null)
                sceneObject.Rotation = item.Rotation;
            if (item.Scale != null)
                sceneObject.Scale = item.Scale;

            applied++;
        }

        return applied > 0;
    }

    private static IEnumerable<(string Category, SceneObjectDto Object)> EnumerateSceneObjects(
        SceneGenerationResponseDto scene)
    {
        foreach (var item in scene.Structures)
            yield return ("structures", item);
        foreach (var item in scene.Props)
            yield return ("props", item);
        foreach (var item in scene.GroundDetails)
            yield return ("ground_details", item);
        foreach (var item in scene.Vegetation)
            yield return ("vegetation", item);
        foreach (var item in scene.Npcs)
            yield return ("npcs", item);
    }

    private static string ClassifyManualAsset(Asset asset)
    {
        var name = asset.Name.ToLowerInvariant();
        if (name.Contains("guard") || name.Contains("priest") || name.Contains("scribe") || name.Contains("merchant"))
            return "npcs";
        if (name.Contains("pyramid") || name.Contains("temple") || name.Contains("hall") ||
            name.Contains("pylon") || name.Contains("obelisk"))
            return "structures";
        if (name.Contains("palm") || name.Contains("tree") || name.Contains("plant") || name.Contains("lotus"))
            return "vegetation";
        if (name.Contains("path") || name.Contains("pavement") || name.Contains("steps") || name.Contains("floor"))
            return "ground_details";

        return "props";
    }

    private static Vector3Dto GetManualPosition(string category, int index)
    {
        return category switch
        {
            "structures" => new Vector3Dto { X = (index - 2) * 8f, Y = 0f, Z = 14f + (index % 2) * 6f },
            "npcs" => new Vector3Dto { X = (index - 1) * 3f, Y = 0f, Z = -4f },
            "vegetation" => PositionOnCircle(index, 16f, 20f),
            "ground_details" => new Vector3Dto { X = (index - 1) * 4f, Y = -0.02f, Z = 0f },
            _ => PositionOnCircle(index, 5f, 8f)
        };
    }

    private static Vector3Dto GetManualRotation(string category, int index)
    {
        return category switch
        {
            "structures" => new Vector3Dto { X = 0f, Y = 180f, Z = 0f },
            "npcs" => new Vector3Dto { X = 0f, Y = 0f, Z = 0f },
            "vegetation" => new Vector3Dto { X = 0f, Y = (index * 47) % 360, Z = 0f },
            _ => new Vector3Dto { X = 0f, Y = (index * 35) % 360, Z = 0f }
        };
    }

    private static Vector3Dto GetManualScale(Asset asset)
    {
        var name = asset.Name.ToLowerInvariant();
        var scale = name.Contains("pyramid") ? 1.4f :
            name.Contains("temple") || name.Contains("hall") || name.Contains("pylon") ? 1.2f :
            1f;

        return new Vector3Dto { X = scale, Y = scale, Z = scale };
    }

    private static Vector3Dto PositionOnCircle(int index, float radius, float stepDegrees)
    {
        var angle = index * stepDegrees * MathF.PI / 180f;
        return new Vector3Dto
        {
            X = MathF.Round(MathF.Cos(angle) * radius, 2),
            Y = 0f,
            Z = MathF.Round(MathF.Sin(angle) * radius, 2)
        };
    }

    private static void AddManualPoints(SceneGenerationResponseDto scene, string goal)
    {
        var landmarkObjects = scene.Structures
            .Concat(scene.Props)
            .Take(7)
            .ToList();

        for (var i = 0; i < landmarkObjects.Count; i++)
        {
            var item = landmarkObjects[i];
            scene.Points.Add(new ScenePointDto
            {
                PointId = $"manual_point_{i + 1}",
                TargetObjectId = item.ObjectId,
                TargetCategory = scene.Structures.Any(s => s.ObjectId == item.ObjectId) ? "structures" : "props",
                IsLandmark = i == 0,
                Position = item.Position,
                Title = GetManualPointTitle(goal, item.Name),
                Description = $"Explore {item.Name} within this manually curated historical scene.",
                Completed = false
            });
        }
    }

    private static string GetManualPointTitle(string goal, string objectName)
    {
        return goal switch
        {
            "Exploratory" => $"Discover {objectName}",
            "Cultural" => $"Observe the cultural role of {objectName}",
            _ => $"Learn about {objectName}"
        };
    }

    private static string ComputeCacheKey(
        string civilization,
        string locationOldName,
        string goal,
        string? roleOrName)
    {
        var raw = $"{civilization}|{locationOldName}|{goal}|{roleOrName ?? ""}".ToLowerInvariant();
        return ComputeHash(raw);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeHash(byte[] input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(input);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("User identity not found.");

    private static string MapGoal(string? inputGoal)
    {
        if (string.IsNullOrWhiteSpace(inputGoal))
            return "Educational";

        var normalized = inputGoal.Trim().ToLowerInvariant();
        if (normalized.Contains("edu") || normalized.Contains("learn") || normalized.Contains("study"))
            return "Educational";
        if (normalized.Contains("explor") || normalized.Contains("find") || normalized.Contains("search"))
            return "Exploratory";
        if (normalized.Contains("cultur") || normalized.Contains("tradition") || normalized.Contains("art") || normalized.Contains("custom"))
            return "Cultural";

        return "Educational";
    }

    private async Task<string> SaveGlbAsync(byte[] fileBytes, string fileName)
    {
        using var stream = new MemoryStream(fileBytes);
        var formFile = new FormFile(stream, 0, fileBytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "model/gltf-binary"
        };

        return await _fileStorageService.UploadFileAsync(formFile, "vr-assets");
    }
}

