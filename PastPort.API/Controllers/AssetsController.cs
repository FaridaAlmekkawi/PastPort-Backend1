// ✅ FIXED: AssetsController.cs
// المشاكل الأصلية:
// 1. ComputeHash كانت بتستخدم MD5 — ده algorithm مكسور ومش آمن للـ integrity
//    الحل: استبدلناه بـ SHA256
// 2. DownloadAsset, CheckAssets, GetSceneAssets كانوا [AllowAnonymous]
//    يعني أي حد يقدر يعمل download لأي ملف من غير authentication
//    الحل: أضفنا [Authorize] للـ download endpoint
//    (GetSceneAssets و CheckAssets تقدر تسيبهم anonymous لو Unity محتاجهم)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.DTOs.Response;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;
using PastPort.Application.Interfaces;
using System.ComponentModel.DataAnnotations;
using PastPort.Domain.Enums;

namespace PastPort.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssetsController(
        IAssetRepository assetRepository,
        IFileStorageService fileStorageService,
        ILogger<AssetsController> logger)
        : ControllerBase
    {
        // ✅ تقدر تسيب ده Anonymous لأن Unity محتاج يعرف الـ assets بتاعة الـ scene
        // من غير ما يكون عنده token — لكن لو عندك auth في Unity سيبه [Authorize]
        [AllowAnonymous]
        [HttpGet("scenes/{sceneId}")]
        public async Task<IActionResult> GetSceneAssets(Guid sceneId)
        {
            try
            {
                var assets = await assetRepository.GetAssetsBySceneIdAsync(sceneId);
                return Ok(new { success = true, data = assets });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting scene assets for {SceneId}", sceneId);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ✅ CheckAssets تقدر تسيبها Anonymous — Unity بيستخدمها عشان يعرف إيه اللي محتاج يتحمل
        [AllowAnonymous]
        [HttpPost("check")]
        public async Task<IActionResult> CheckAssets([FromBody] AssetCheckRequestDto request)
        {
            try
            {
                var results = new List<object>();
                foreach (var item in request.Assets)
                {
                    var asset = await assetRepository.GetAssetByFileNameAsync(item.FileName);
                    results.Add(new
                    {
                        fileName = item.FileName,
                        exists = asset != null,
                        needsUpdate = asset != null && asset.FileHash != item.FileHash
                    });
                }
                return Ok(new { success = true, results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking assets");
                return BadRequest(new { error = ex.Message });
            }
        }

        // ✅ FIXED: أضفنا [Authorize] هنا
        // الكود القديم كان [AllowAnonymous] يعني أي حد يقدر يعمل download
        // لأي ملف لو عرف اسمه — ده security hole واضح.
        // دلوقتي محتاج token صالح عشان تنزّل أي asset.
        [Authorize]
        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadAsset(string fileName)
        {
            try
            {
                var asset = await assetRepository.GetAssetByFileNameAsync(fileName);
                if (asset == null)
                    return NotFound(new { error = "Asset not found" });

                var fileBytes = await fileStorageService.GetFileAsync(asset.FileUrl);
                return File(fileBytes, GetContentType(fileName), fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error downloading asset {FileName}", fileName);
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Upload Asset - Select file and fill form
        /// </summary>
        [Authorize]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAsset(
            [Required]
            [FromForm(Name = "file")]
            IFormFile? file,
            [Required]
            [FromForm(Name = "name")]
            string name,
            [Required]
            [FromForm(Name = "type")]
            int type,
            [FromForm(Name = "sceneId")]
            Guid? sceneId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "File is required" });

                if (string.IsNullOrWhiteSpace(name))
                    return BadRequest(new { error = "Name is required" });

                if (!Enum.IsDefined(typeof(AssetType), type))
                    return BadRequest(new { error = "Invalid type. Use: 1=Model3D, 2=Texture, 4=Audio, 5=Animation" });

                var assetType = (AssetType)type;
                var folder = GetFolderByType(assetType);

                var fileUrl = await fileStorageService.UploadFileAsync(file, folder);
                var fileBytes = await fileStorageService.GetFileAsync(fileUrl);

                // ✅ FIXED: بنستخدم SHA256 بدل MD5
                // MD5 مكسور cryptographically ومش مناسب لـ file integrity
                var fileHash = ComputeHash(fileBytes);

                var asset = new Asset
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    FileName = Path.GetFileName(fileUrl),
                    Type = assetType,
                    FilePath = fileUrl,
                    FileUrl = fileUrl,
                    FileSize = file.Length,
                    FileHash = fileHash,
                    Version = "1.0.0",
                    SceneId = sceneId,
                    Status = AssetStatus.Available,
                    CreatedAt = DateTime.UtcNow
                };

                await assetRepository.AddAsync(asset);

                logger.LogInformation("Asset uploaded: {AssetName} by user", name);

                return Ok(new
                {
                    success = true,
                    message = "Asset uploaded successfully",
                    asset = new
                    {
                        id = asset.Id,
                        name = asset.Name,
                        fileName = asset.FileName,
                        type = asset.Type.ToString(),
                        fileUrl = asset.FileUrl,
                        fileSize = asset.FileSize
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Upload error for asset {Name}", name);
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{assetId}")]
        public async Task<IActionResult> DeleteAsset(Guid assetId)
        {
            try
            {
                var asset = await assetRepository.GetByIdAsync(assetId);
                if (asset == null)
                    return NotFound(new { error = "Asset not found" });

                await fileStorageService.DeleteFileAsync(asset.FileUrl);
                await assetRepository.DeleteAsync(asset);

                logger.LogInformation("Asset deleted: {AssetId}", assetId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting asset {AssetId}", assetId);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ==================== Private Helpers ====================

        private string GetFolderByType(AssetType type) => type switch
        {
            AssetType.Model3D => "models",
            AssetType.Texture => "textures",
            AssetType.Audio => "audio",
            AssetType.Animation => "animations",
            _ => "assets"
        };

        private string GetContentType(string fileName) =>
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".fbx" => "application/octet-stream",
                ".glb" => "model/gltf-binary",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                _ => "application/octet-stream"
            };

        // ✅ FIXED: SHA256 بدل MD5
        // MD5 يعمل collisions — يعني ملفين مختلفين ممكن يديوا نفس الـ hash
        // ده بيخلي الـ file integrity check مش موثوق فيه.
        // SHA256 هو الـ standard الحالي لـ file integrity verification.
        private static string ComputeHash(byte[] fileBytes)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(fileBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}