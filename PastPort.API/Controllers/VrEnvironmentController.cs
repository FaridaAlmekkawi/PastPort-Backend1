using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VrEnvironmentController : ControllerBase
{
    private readonly IVrEnvironmentService _vrService;
    private readonly IAssetRepository _assetRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<VrEnvironmentController> _logger;

    public VrEnvironmentController(
        IVrEnvironmentService vrService,
        IAssetRepository assetRepository,
        IFileStorageService fileStorageService,
        ILogger<VrEnvironmentController> logger)
    {
        _vrService = vrService;
        _assetRepository = assetRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var healthy = await _vrService.CheckHealthAsync();
        return Ok(new { healthy });
    }

    [HttpGet("scene")]
    public async Task<IActionResult> GetScene(
        [FromQuery] string civilization,
        [FromQuery] string yearRange,
        [FromQuery] string locationOldName,
        [FromQuery] string? roleOrName = null)
    {
        try
        {
            var scene = await _vrService.GenerateSceneAsync(
                civilization, yearRange, locationOldName, roleOrName);

            return Ok(new { success = true, data = scene });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate scene");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    [HttpGet("asset")]
    public async Task<IActionResult> GetAsset(
        [FromQuery] string prompt,
        [FromQuery] bool isNpc = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { error = "Prompt is required" });

        var promptHash = ComputeHash(prompt);

        // 1) دوّر في الكاش الأول قبل ما تكلّف credits
        var cached = await _assetRepository.GetAssetByPromptHashAsync(promptHash);
        if (cached != null)
        {
            _logger.LogInformation("Asset cache HIT for prompt hash {Hash}", promptHash);
            return Ok(new
            {
                success = true,
                cached = true,
                data = new { fileUrl = cached.FileUrl, assetId = cached.Id }
            });
        }

        // 2) مش موجود → استدعي السيرفر الخارجي (بيكلّف credit)
        _logger.LogInformation("Asset cache MISS, generating via external API");

        Stream glbStream;
        try
        {
            glbStream = await _vrService.GenerateAssetAsync(prompt, isNpc, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate asset");
            return StatusCode(502, new { error = ex.Message });
        }

        using var ms = new MemoryStream();
        await glbStream.CopyToAsync(ms, cancellationToken);
        var fileBytes = ms.ToArray();

        // 3) خزّن الملف باستخدام نفس IFileStorageService الموجود عندك
        var fileName = $"{promptHash}.glb";
        var fileUrl = await SaveGlbAsync(fileBytes, fileName);

        // 4) اعمل Asset record جديد عشان المرة الجاية يبقى cached
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

        return Ok(new
        {
            success = true,
            cached = false,
            data = new { fileUrl = asset.FileUrl, assetId = asset.Id }
        });
    }

    // ==================== Private Helpers ====================

    private async Task<string> SaveGlbAsync(byte[] fileBytes, string fileName)
    {
        // IFileStorageService محتاج IFormFile مش byte[]، فهنلفها هنا
        using var stream = new MemoryStream(fileBytes);
        var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, fileBytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "model/gltf-binary"
        };

        return await _fileStorageService.UploadFileAsync(formFile, "vr-assets");
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}