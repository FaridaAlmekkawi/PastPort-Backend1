using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Infrastructure.Data;

namespace PastPort.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/local-assets")]
public class LocalAssetsController(
    ApplicationDbContext context,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("import-outdoor-decorations")]
    public async Task<IActionResult> ImportOutdoorDecorations()
    {
        const string relativeFolder = "uploads/assets/low-poly-outdoor-decorations-glb";
        var absoluteFolder = Path.Combine(environment.WebRootPath, relativeFolder);

        if (!Directory.Exists(absoluteFolder))
            return NotFound(new { message = "Outdoor decorations folder was not found" });

        var files = Directory.GetFiles(absoluteFolder, "*.glb");
        var imported = 0;
        var skipped = 0;

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var exists = await context.Assets.AnyAsync(a => a.FileName == fileName);

            if (exists)
            {
                skipped++;
                continue;
            }

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileNameWithoutExtension(fileName),
                FileName = fileName,
                Type = AssetType.Model3D,
                FilePath = $"/{relativeFolder.Replace("\\", "/")}/{fileName}",
                FileUrl = $"/{relativeFolder.Replace("\\", "/")}/{fileName}",
                FileSize = new FileInfo(file).Length,
                FileHash = await ComputeSha256Async(file),
                Version = "1.0.0",
                Status = AssetStatus.Available,
                Description = "Low poly outdoor decoration GLB asset",
                Tags = "vr,outdoor,decoration,low-poly,glb",
                CreatedAt = DateTime.UtcNow
            };

            context.Assets.Add(asset);
            imported++;
        }

        await context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                folder = $"/{relativeFolder.Replace("\\", "/")}",
                imported,
                skipped,
                total = files.Length
            }
        });
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = System.IO.File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
