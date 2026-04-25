// ✅ FIXED: UnityAssetsController.cs
// المشاكل الأصلية:
// 1. SearchAsset كانت بتعمل GetAllAsync() وتحمّل كل الـ assets في الـ memory
//    وبعدين تعمل FirstOrDefault في الـ C# — لو عندك 10,000 asset هيتحملوا كلهم!
//    الحل: استخدمنا GetAssetByNameAsync اللي بتعمل WHERE في الـ database.
// 2. DownloadAsset كانت [AllowAnonymous] — أي حد يعرف الـ assetId يقدر يحمّل أي ملف.
//    الحل: أضفنا [Authorize] عليه.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using PastPort.Domain.Interfaces;

namespace PastPort.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnityAssetsController(
    IAssetRepository assetRepository,
    IFileStorageService fileStorageService,
    ILogger<UnityAssetsController> logger)
    : ControllerBase
{
    /// <summary>
    /// البحث عن Asset بالاسم
    /// Unity استخدم: GET /api/unityassets/search?name=chair_01
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous] // Unity محتاج يبحث من غير token — مقبول هنا
    public async Task<IActionResult> SearchAsset([FromQuery] string name)
    {
        try
        {
            if (string.IsNullOrEmpty(name))
                return BadRequest(new { error = "Asset name is required" });

            // ✅ FIXED: الكود القديم كان:
            //   var assets = await _assetRepository.GetAllAsync();  ← بيحمّل كل الـ assets في الـ memory
            //   var asset = assets.FirstOrDefault(a => a.Name.Equals(...)); ← بيفلتر في C# مش في DB
            //
            // لو عندك 10,000 asset، كلهم بيتحملوا في الـ RAM عشان تجيب واحد بس!
            // دلوقتي: بنبعت الـ filter للـ database مباشرة بـ WHERE clause
            var asset = await assetRepository.GetAssetByNameAsync(name);

            if (asset == null)
                return NotFound(new { error = "Asset not found" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = asset.Id,
                    name = asset.Name,
                    fileName = asset.FileName,
                    type = asset.Type.ToString(),
                    fileUrl = asset.FileUrl,
                    fileSize = asset.FileSize,
                    fileHash = asset.FileHash,
                    version = asset.Version
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching asset with name {Name}", name);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// الحصول على جميع Assets الخاصة بـ Scene
    /// Unity استخدم: GET /api/unityassets/scene/sceneId
    /// </summary>
    [HttpGet("scene/{sceneId}")]
    [AllowAnonymous] // Unity محتاج يجيب assets الـ scene من غير token
    public async Task<IActionResult> GetSceneAssets(Guid sceneId)
    {
        try
        {
            var assets = await assetRepository.GetAssetsBySceneIdAsync(sceneId);

            return Ok(new
            {
                success = true,
                data = assets.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    fileName = a.FileName,
                    type = a.Type.ToString(),
                    fileUrl = a.FileUrl,
                    fileSize = a.FileSize,
                    fileHash = a.FileHash,
                    version = a.Version
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting scene assets for {SceneId}", sceneId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// تحميل Asset
    /// Unity استخدم: GET /api/unityassets/download/assetId
    /// </summary>
    // ✅ FIXED: أضفنا [Authorize] بدل [AllowAnonymous]
    // الكود القديم كان بيسمح لأي حد يعرف الـ assetId يحمّل أي ملف بدون أي auth.
    // دلوقتي محتاج JWT token صالح عشان تحمّل.
    // ملاحظة: لو Unity client بيحتاج download بدون user token،
    // استخدم API key authentication بدل JWT.
    [HttpGet("download/{assetId}")]
    [Authorize]
    public async Task<IActionResult> DownloadAsset(Guid assetId)
    {
        try
        {
            var asset = await assetRepository.GetByIdAsync(assetId);
            if (asset == null)
                return NotFound(new { error = "Asset not found" });

            if (!fileStorageService.FileExists(asset.FileUrl))
                return NotFound(new { error = "File not found on server" });

            var fileBytes = await fileStorageService.GetFileAsync(asset.FileUrl);
            var contentType = GetContentType(asset.FileName);

            logger.LogInformation("Asset downloaded: {AssetId}", assetId);

            return File(fileBytes, contentType, asset.FileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading asset {AssetId}", assetId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// التحقق من وجود Asset وحالته
    /// Unity استخدم: POST /api/unityassets/verify
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous] // Unity محتاج يتحقق من الـ assets قبل ما يبدأ الـ session
    public async Task<IActionResult> VerifyAsset([FromBody] VerifyAssetRequest request)
    {
        try
        {
            var asset = await assetRepository.GetByIdAsync(request.AssetId);
            if (asset == null)
                return NotFound(new { error = "Asset not found" });

            var fileExists = fileStorageService.FileExists(asset.FileUrl);
            var hashMatches = asset.FileHash == request.FileHash;

            return Ok(new
            {
                success = true,
                data = new
                {
                    exists = fileExists,
                    hashMatches,
                    needsDownload = !fileExists || !hashMatches,
                    asset = new
                    {
                        id = asset.Id,
                        name = asset.Name,
                        fileUrl = asset.FileUrl,
                        fileHash = asset.FileHash,
                        version = asset.Version
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying asset {AssetId}", request.AssetId);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ==================== Private Helper ====================

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".fbx" => "application/octet-stream",
            ".obj" => "application/octet-stream",
            ".glb" => "model/gltf-binary",
            ".gltf" => "model/gltf+json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };
    }
}

public class VerifyAssetRequest
{
    public Guid AssetId { get; set; }
    public string FileHash { get; set; } = string.Empty;
}