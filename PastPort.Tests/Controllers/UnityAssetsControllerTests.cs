using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PastPort.API.Controllers;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Domain.Interfaces;
using Xunit;

namespace PastPort.Tests.Controllers;

public sealed class UnityAssetsControllerTests
{
    private readonly Mock<IAssetRepository> _assetRepo = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly UnityAssetsController _sut;

    public UnityAssetsControllerTests()
    {
        _sut = new UnityAssetsController(
            _assetRepo.Object,
            _fileStorage.Object,
            NullLogger<UnityAssetsController>.Instance);
    }

    [Fact]
    public async Task SearchAsset_WhenFound_ReturnsOk()
    {
        // Arrange
        var assetName = "testAsset";
        var asset = new Asset { Id = Guid.NewGuid(), Name = assetName, Type = AssetType.Model3D };
        _assetRepo.Setup(r => r.GetAssetByNameAsync(assetName)).ReturnsAsync(asset);

        // Act
        var result = await _sut.SearchAsset(assetName);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsset_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _assetRepo.Setup(r => r.GetAssetByNameAsync(It.IsAny<string>())).ReturnsAsync((Asset?)null);

        // Act
        var result = await _sut.SearchAsset("ghost");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task VerifyAsset_WhenValid_ReturnsSuccess()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var hash = "hash123";
        var asset = new Asset { Id = assetId, FileHash = hash, FileUrl = "url" };
        
        _assetRepo.Setup(r => r.GetByIdAsync(assetId)).ReturnsAsync(asset);
        _fileStorage.Setup(s => s.FileExists("url")).Returns(true);

        var request = new VerifyAssetRequest { AssetId = assetId, FileHash = hash };

        // Act
        var result = await _sut.VerifyAsset(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
    }
}
