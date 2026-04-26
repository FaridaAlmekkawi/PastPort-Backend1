using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PastPort.API.Controllers;
using PastPort.Application.Interfaces;
using PastPort.Application.Models.Npc;
using Xunit;

namespace PastPort.Tests.Controllers;

public sealed class NpcSessionControllerTests
{
    private readonly Mock<ICacheService> _cache = new();
    private readonly NpcSessionController _sut;

    public NpcSessionControllerTests()
    {
        _sut = new NpcSessionController(
            _cache.Object,
            NullLogger<NpcSessionController>.Instance);
    }

    [Fact]
    public void StartSession_ReturnsCreatedWithSessionId()
    {
        // Arrange
        var request = new StartSessionRequest("1900-1950", "Old Cairo", "Islamic");

        // Act
        var result = _sut.StartSession(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<StartSessionResponse>().Subject;
        
        response.SessionId.Should().NotBeNullOrEmpty();
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        _cache.Verify(c => c.Set(
            It.Is<string>(k => k.StartsWith("npc:session:")),
            It.Is<NpcSessionData>(d => 
                d.YearRange == request.YearRange && 
                d.LocationOldName == request.LocationOldName && 
                d.Civilization == request.Civilization),
            It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public void GetSession_WhenSessionExists_ReturnsOk()
    {
        // Arrange
        var sessionId = "testSession";
        var cacheKey = NpcSessionController.BuildCacheKey(sessionId);
        var sessionData = new NpcSessionData("1900", "Place", "Civ", DateTime.UtcNow);
        
        _cache.Setup(c => c.TryGetValue(cacheKey, out sessionData)).Returns(true);

        // Act
        var result = _sut.GetSession(sessionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetSession_WhenSessionMissing_ReturnsNotFound()
    {
        // Arrange
        var sessionId = "missingSession";
        var cacheKey = NpcSessionController.BuildCacheKey(sessionId);
        NpcSessionData? sessionData = null;
        
        _cache.Setup(c => c.TryGetValue(cacheKey, out sessionData)).Returns(false);

        // Act
        var result = _sut.GetSession(sessionId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void BuildCacheKey_ReturnsCorrectFormat()
    {
        // Act
        var key = NpcSessionController.BuildCacheKey("123");

        // Assert
        key.Should().Be("npc:session:123");
    }
}
