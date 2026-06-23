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
    private readonly ILogger<VrEnvironmentController> _logger;

    // كام يوم الـ scene cache يفضل صالح
    private const int SceneCacheDays = 7;

    public VrEnvironmentController(
        IVrEnvironmentService vrService,
        IAssetRepository assetRepository,
        ISceneCacheRepository sceneCacheRepository,
        IFileStorageService fileStorageService,
        ILogger<VrEnvironmentController> logger)
    {
        _vrService = vrService;
        _assetRepository = assetRepository;
        _sceneCacheRepository = sceneCacheRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    // ============================================================
    // Flutter بيستدعي ده أول حاجة — يرجع sessionId
    // ============================================================
    [HttpPost("session")]
    public async Task<IActionResult> StartSession([FromBody] StartVrSessionRequest request)
    {
        var sessionId = Guid.NewGuid().ToString();

        var session = new VrSession
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Civilization = request.Civilization,
            YearRange = request.YearRange,
            LocationOldName = request.LocationOldName,
            RoleOrName = request.RoleOrName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(4)
        };

        // خزّن السيشن مؤقتًا في الـ DB
        // (لو عندك Redis ممكن تخزنه هناك بدل الـ DB)
        using var context = HttpContext.RequestServices
            .GetRequiredService<PastPort.Infrastructure.Data.ApplicationDbContext>();
        context.VrSessions.Add(session);
        await context.SaveChangesAsync();

        _logger.LogInformation(
            "VR Session started: {SessionId} | Civ: {Civ}",
            sessionId, request.Civilization);

        return Ok(new
        {
            success = true,
            data = new { sessionId }
        });
    }

    // ============================================================
    // Unity بيستدعي ده بالـ sessionId — بيجيب الـ scene
    // ============================================================
    [HttpGet("scene/{sessionId}")]
    public async Task<IActionResult> GetScene(string sessionId)
    {
        // 1) جيب الـ session من الـ DB
        using var context = HttpContext.RequestServices
            .GetRequiredService<PastPort.Infrastructure.Data.ApplicationDbContext>();

        var session = await context.VrSessions
            .FirstOrDefaultAsync(s =>
                s.SessionId == sessionId &&
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
            "Scene cache MISS, generating: {Civ} {Year}",
            session.Civilization, session.YearRange);

        try
        {
            var scene = await _vrService.GenerateSceneAsync(
                session.Civilization,
                session.YearRange,
                session.LocationOldName,
                session.RoleOrName);

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
            FileHash = ComputeHash(Convert.ToBase64String(fileBytes)),
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

// Request DTOs (في نفس الملف أو في Application/DTOs/Request)
public class StartVrSessionRequest
{
    public string Civilization { get; set; } = string.Empty;
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string? RoleOrName { get; set; }
}