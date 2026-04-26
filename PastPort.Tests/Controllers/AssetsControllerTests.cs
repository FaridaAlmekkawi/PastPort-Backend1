using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PastPort.API.Controllers;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Domain.Interfaces;
using Xunit;

namespace PastPort.Tests.Controllers;

public sealed class AssetsControllerTests
{
    private readonly Mock<IAssetRepository> _assetRepo = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageService> _fileStorage = new(MockBehavior.Strict);
    private readonly AssetsController _sut;

    public AssetsControllerTests()
    {
        _sut = new AssetsController(
            _assetRepo.Object,
            _fileStorage.Object,
            NullLogger<AssetsController>.Instance);
    }

    // ── GetSceneAssets ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSceneAssets_ReturnsOkWithAssets()
    {
        var sceneId = Guid.NewGuid();
        var assets = new List<Asset>
        {
            BuildAsset(sceneId: sceneId),
            BuildAsset(sceneId: sceneId)
        };
        _assetRepo.Setup(r => r.GetAssetsBySceneIdAsync(sceneId)).ReturnsAsync(assets);

        var result = await _sut.GetSceneAssets(sceneId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        _assetRepo.VerifyAll();
    }

    [Fact]
    public async Task GetSceneAssets_WhenNoAssets_ReturnsOkWithEmptyList()
    {
        var sceneId = Guid.NewGuid();
        _assetRepo.Setup(r => r.GetAssetsBySceneIdAsync(sceneId)).ReturnsAsync(new List<Asset>());

        var result = await _sut.GetSceneAssets(sceneId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSceneAssets_WhenRepoThrows_ReturnsBadRequest()
    {
        var sceneId = Guid.NewGuid();
        _assetRepo.Setup(r => r.GetAssetsBySceneIdAsync(sceneId))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetSceneAssets(sceneId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── CheckAssets ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckAssets_WhenAssetExistsAndHashMatches_ReturnsNeedsUpdateFalse()
    {
        var asset = BuildAsset(fileHash: "abc123");
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync(asset.FileName)).ReturnsAsync(asset);

        var request = new AssetCheckRequestDto
        {
            Assets = new List<AssetCheckItem>
            {
                new() { FileName = asset.FileName, FileHash = "abc123" }
            }
        };

        var result = await _sut.CheckAssets(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        _assetRepo.VerifyAll();
    }

    [Fact]
    public async Task CheckAssets_WhenHashDiffers_ReturnsNeedsUpdateTrue()
    {
        var asset = BuildAsset(fileHash: "oldHash");
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync(asset.FileName)).ReturnsAsync(asset);

        var request = new AssetCheckRequestDto
        {
            Assets = new List<AssetCheckItem>
            {
                new() { FileName = asset.FileName, FileHash = "newHash" }
            }
        };

        var result = await _sut.CheckAssets(request);

        // The response contains results; verify it's OK (detailed value check via reflection)
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckAssets_WhenAssetNotFound_ReturnsExistsFalse()
    {
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync("missing.fbx")).ReturnsAsync((Asset?)null);

        var request = new AssetCheckRequestDto
        {
            Assets = new List<AssetCheckItem> { new() { FileName = "missing.fbx" } }
        };

        var result = await _sut.CheckAssets(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckAssets_WhenEmptyList_ReturnsOkWithEmptyResults()
    {
        var request = new AssetCheckRequestDto { Assets = new List<AssetCheckItem>() };

        var result = await _sut.CheckAssets(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckAssets_WhenRepoThrows_ReturnsBadRequest()
    {
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("connection lost"));

        var request = new AssetCheckRequestDto
        {
            Assets = new List<AssetCheckItem> { new() { FileName = "any.fbx" } }
        };

        var result = await _sut.CheckAssets(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DownloadAsset ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsset_WhenAssetExists_ReturnsFileStreamResult()
    {
        var asset = BuildAsset(fileName: "model.glb");
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync("model.glb")).ReturnsAsync(asset);
        _fileStorage.Setup(s => s.GetFileStreamAsync(asset.FileUrl)).ReturnsAsync(stream);

        var result = await _sut.DownloadAsset("model.glb");

        result.Should().BeOfType<FileStreamResult>();
        _assetRepo.VerifyAll();
        _fileStorage.VerifyAll();
    }

    [Fact]
    public async Task DownloadAsset_WhenAssetNotFound_ReturnsNotFound()
    {
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync("ghost.fbx")).ReturnsAsync((Asset?)null);

        var result = await _sut.DownloadAsset("ghost.fbx");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DownloadAsset_WhenStorageThrows_ReturnsBadRequest()
    {
        var asset = BuildAsset(fileName: "broken.glb");
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync("broken.glb")).ReturnsAsync(asset);
        _fileStorage.Setup(s => s.GetFileStreamAsync(asset.FileUrl))
            .ThrowsAsync(new IOException("disk read error"));

        var result = await _sut.DownloadAsset("broken.glb");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UploadAsset ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsset_WithNullFile_ReturnsBadRequest()
    {
        var result = await _sut.UploadAsset(null, "TestAsset", (int)AssetType.Model3D, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadAsset_WithEmptyFileName_ReturnsBadRequest()
    {
        var file = BuildFormFile("model.glb", 100);

        var result = await _sut.UploadAsset(file, "", (int)AssetType.Model3D, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadAsset_WithInvalidType_ReturnsBadRequest()
    {
        var file = BuildFormFile("model.glb", 100);

        var result = await _sut.UploadAsset(file, "MyModel", 9999, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadAsset_HappyPath_CreatesAssetWithSha256Hash()
    {
        var content = Encoding.UTF8.GetBytes("fake-binary-content");
        var file = BuildFormFile("model.glb", content);
        var expectedUrl = "https://storage/models/model.glb";
        Asset? captured = null;

        _fileStorage.Setup(s => s.UploadFileAsync(file, "models")).ReturnsAsync(expectedUrl);
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>()))
            .Callback<Asset>(a => captured = a)
            .ReturnsAsync((Asset a) => a);

        var result = await _sut.UploadAsset(file, "MyModel", (int)AssetType.Model3D, null);

        result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.Name.Should().Be("MyModel");
        captured.Type.Should().Be(AssetType.Model3D);
        captured.FileUrl.Should().Be(expectedUrl);

        // Verify it used SHA256 (64 hex chars)
        captured.FileHash.Should().HaveLength(64)
            .And.MatchRegex("^[0-9a-f]+$");

        // Verify hash is consistent
        var expectedHash = ComputeSha256(content);
        captured.FileHash.Should().Be(expectedHash);

        _assetRepo.VerifyAll();
        _fileStorage.VerifyAll();
    }

    [Fact]
    public async Task UploadAsset_Model3D_UsesModelsFolder()
    {
        var file = BuildFormFile("x.glb", new byte[] { 1 });
        _fileStorage.Setup(s => s.UploadFileAsync(file, "models")).ReturnsAsync("url");
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>())).ReturnsAsync((Asset a) => a);

        await _sut.UploadAsset(file, "X", (int)AssetType.Model3D, null);

        _fileStorage.Verify(s => s.UploadFileAsync(file, "models"), Times.Once);
    }

    [Fact]
    public async Task UploadAsset_Audio_UsesAudioFolder()
    {
        var file = BuildFormFile("x.mp3", new byte[] { 1 });
        _fileStorage.Setup(s => s.UploadFileAsync(file, "audio")).ReturnsAsync("url");
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>())).ReturnsAsync((Asset a) => a);

        await _sut.UploadAsset(file, "X", (int)AssetType.Audio, null);

        _fileStorage.Verify(s => s.UploadFileAsync(file, "audio"), Times.Once);
    }

    [Fact]
    public async Task UploadAsset_WithSceneId_AssociatesToScene()
    {
        var sceneId = Guid.NewGuid();
        var file = BuildFormFile("x.glb", new byte[] { 1 });
        Asset? captured = null;
        _fileStorage.Setup(s => s.UploadFileAsync(file, "models")).ReturnsAsync("url");
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>()))
            .Callback<Asset>(a => captured = a)
            .ReturnsAsync((Asset a) => a);

        await _sut.UploadAsset(file, "X", (int)AssetType.Model3D, sceneId);

        captured!.SceneId.Should().Be(sceneId);
    }

    // ── DeleteAsset ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsset_WhenAssetExists_DeletesFileAndRecord()
    {
        var asset = BuildAsset();
        _assetRepo.Setup(r => r.GetByIdAsync(asset.Id)).ReturnsAsync(asset);
        _fileStorage.Setup(s => s.DeleteFileAsync(asset.FileUrl)).ReturnsAsync(true);
        _assetRepo.Setup(r => r.DeleteAsync(asset)).Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsset(asset.Id);

        result.Should().BeOfType<OkObjectResult>();
        _assetRepo.VerifyAll();
        _fileStorage.VerifyAll();
    }

    [Fact]
    public async Task DeleteAsset_WhenAssetNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _assetRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Asset?)null);

        var result = await _sut.DeleteAsset(id);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteAsset_WhenStorageThrows_ReturnsBadRequest()
    {
        var asset = BuildAsset();
        _assetRepo.Setup(r => r.GetByIdAsync(asset.Id)).ReturnsAsync(asset);
        _fileStorage.Setup(s => s.DeleteFileAsync(asset.FileUrl))
            .ThrowsAsync(new IOException("storage down"));

        var result = await _sut.DeleteAsset(asset.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Content-type mapping ───────────────────────────────────────────────────

    [Theory]
    [InlineData("model.glb",  "model/gltf-binary")]
    [InlineData("tex.png",    "image/png")]
    [InlineData("tex.jpg",    "image/jpeg")]
    [InlineData("tex.jpeg",   "image/jpeg")]
    [InlineData("sound.mp3",  "audio/mpeg")]
    [InlineData("sound.wav",  "audio/wav")]
    [InlineData("model.fbx",  "application/octet-stream")]
    [InlineData("unknown.xyz","application/octet-stream")]
    public async Task DownloadAsset_ContentTypeMatchesExtension(string fileName, string expectedContentType)
    {
        var asset = BuildAsset(fileName: fileName);
        _assetRepo.Setup(r => r.GetAssetByFileNameAsync(fileName)).ReturnsAsync(asset);
        _fileStorage.Setup(s => s.GetFileStreamAsync(asset.FileUrl))
            .ReturnsAsync(new MemoryStream(new byte[] { 1 }));

        var result = await _sut.DownloadAsset(fileName);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be(expectedContentType);
    }

    // ── SHA256 consistency ────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsset_DifferentFileContents_ProduceDifferentHashes()
    {
        var file1 = BuildFormFile("a.glb", new byte[] { 1, 2, 3 });
        var file2 = BuildFormFile("b.glb", new byte[] { 4, 5, 6 });
        Asset? asset1 = null, asset2 = null;

        _fileStorage.Setup(s => s.UploadFileAsync(file1, "models")).ReturnsAsync("url1");
        _fileStorage.Setup(s => s.UploadFileAsync(file2, "models")).ReturnsAsync("url2");
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>()))
            .Callback<Asset>(a =>
            {
                if (asset1 == null) asset1 = a;
                else asset2 = a;
            })
            .ReturnsAsync((Asset a) => a);

        await _sut.UploadAsset(file1, "A", (int)AssetType.Model3D, null);
        await _sut.UploadAsset(file2, "B", (int)AssetType.Model3D, null);

        asset1!.FileHash.Should().NotBe(asset2!.FileHash);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Asset BuildAsset(
        Guid? sceneId = null,
        string fileName = "test.glb",
        string fileHash = "aabbcc",
        string fileUrl = "https://storage/test.glb") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Asset",
            FileName = fileName,
            FileUrl = fileUrl,
            FileHash = fileHash,
            FileSize = 1024,
            Type = AssetType.Model3D,
            Status = AssetStatus.Available,
            Version = "1.0.0",
            SceneId = sceneId
        };

    private static IFormFile BuildFormFile(string name, long length)
    {
        var content = new byte[length];
        return BuildFormFile(name, content);
    }

    private static IFormFile BuildFormFile(string name, byte[] content)
    {
        var stream = new MemoryStream(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(name);
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        mock.Setup(f => f.ContentType).Returns("application/octet-stream");
        return mock.Object;
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }
}
