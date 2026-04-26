using System;
using System.Reflection;
using FluentAssertions;
using Mapster;
using MapsterMapper;
using PastPort.Application.DTOs.Response;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using Xunit;

namespace PastPort.Tests.Services;

public class MappingTests
{
    private readonly IMapper _mapper;

    public MappingTests()
    {
        var config = new TypeAdapterConfig();
        // Scan the assembly where mappings are defined
        config.Scan(Assembly.Load("PastPort.Application"));
        _mapper = new Mapper(config);
    }

    [Fact]
    public void ApplicationUser_To_UserDto_ShouldMapCorrectly()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            IsEmailVerified = true,
            LastLoginAt = DateTime.UtcNow
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.Id.Should().Be(user.Id);
        dto.FirstName.Should().Be(user.FirstName);
        dto.LastName.Should().Be(user.LastName);
        dto.Email.Should().Be(user.Email);
        dto.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public void Asset_To_AssetDto_ShouldMapCorrectly()
    {
        var assetId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var asset = new Asset
        {
            Id = assetId,
            Name = "Viking Axe",
            FileName = "axe.glb",
            FileUrl = "https://cdn.com/axe.glb",
            FileHash = "sha256hash",
            FileSize = 2048,
            Type = AssetType.Model3D,
            Status = AssetStatus.Available,
            Version = "1.0.0",
            SceneId = sceneId,
            CreatedAt = DateTime.UtcNow
        };

        var dto = _mapper.Map<AssetDto>(asset);

        dto.Id.Should().Be(asset.Id);
        dto.Name.Should().Be(asset.Name);
        dto.FileUrl.Should().Be(asset.FileUrl);
        dto.Type.Should().Be(asset.Type.ToString());
        dto.SceneId.Should().Be(sceneId);
    }
}
