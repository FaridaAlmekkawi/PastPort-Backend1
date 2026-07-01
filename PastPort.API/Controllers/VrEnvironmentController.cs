using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
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

        // 2) احسب الـ cache key
        var cacheKey = ComputeCacheKey(
            session.Civilization,
            session.YearRange,
            session.LocationOldName,
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
            var experience = await _context.UserExperiences
                .FirstOrDefaultAsync(ue => ue.VrSessionId == session.SessionId);
            var goal = MapGoal(experience?.Goal);

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

    private static string ComputeCacheKey(
        string civilization,
        string yearRange,
        string locationOldName,
        string? roleOrName)
    {
        var raw = $"{civilization}|{yearRange}|{locationOldName}|{roleOrName ?? ""}".ToLowerInvariant();
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
