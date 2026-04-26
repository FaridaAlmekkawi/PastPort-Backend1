using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PastPort.API.Hubs;
using PastPort.Application.Interfaces;
using PastPort.Application.Models.Npc;
using PastPort.API.Controllers;
using Xunit;
using FluentAssertions;

namespace PastPort.Tests.Hubs;

public sealed class NpcHubTests
{
    private readonly Mock<INpcAIService> _aiService = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IHubCallerClients> _clients = new();
    private readonly Mock<ISingleClientProxy> _caller = new();
    private readonly Mock<HubCallerContext> _context = new();
    private readonly NpcHub _sut;

    public NpcHubTests()
    {
        _clients.Setup(c => c.Caller).Returns(_caller.Object);
        _context.Setup(c => c.ConnectionId).Returns("test-conn");
        _context.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        _sut = new NpcHub(_aiService.Object, _cache.Object, NullLogger<NpcHub>.Instance)
        {
            Clients = _clients.Object,
            Context = _context.Object
        };
    }

    [Fact]
    public async Task StartConversation_WhenSessionNotFound_SendsError()
    {
        // Arrange
        var sessionId = "bad-session";
        var cacheKey = NpcSessionController.BuildCacheKey(sessionId);
        _cache.Setup(c => c.TryGetValue(cacheKey, out It.Ref<NpcSessionData?>.IsAny)).Returns(false);

        // Act
        await _sut.StartConversation(sessionId, "Role", GetEmptyStream());

        // Assert
        _caller.Verify(c => c.SendCoreAsync("OnSessionError", 
            It.Is<object[]>(o => o[0].ToString()!.Contains("Session not found")), 
            default), Times.Once);
    }

    [Fact]
    public async Task StartConversation_HappyPath_StreamsChunks()
    {
        // Arrange
        var sessionId = "good-session";
        var cacheKey = NpcSessionController.BuildCacheKey(sessionId);
        var sessionData = new NpcSessionData("1900-1920", "Cairo", "Egyptian", DateTime.UtcNow);
        
        _cache.Setup(c => c.TryGetValue(cacheKey, out sessionData)).Returns(true);
        
        var audioBytes = new byte[] { 1, 2, 3 };
        var inputChunks = new List<byte[]> { audioBytes };

        _aiService.Setup(s => s.StreamConversationAsync(
                It.IsAny<byte[]>(), sessionData, "Hero", It.IsAny<CancellationToken>()))
            .Returns(GetAiChunks());

        // Act
        await _sut.StartConversation(sessionId, "Hero", GetAsyncStream(inputChunks));

        // Assert
        _caller.Verify(c => c.SendCoreAsync("OnMetaReceived", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _caller.Verify(c => c.SendCoreAsync("OnAudioReceived", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _caller.Verify(c => c.SendCoreAsync("OnConversationDone", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private async IAsyncEnumerable<byte[]> GetEmptyStream()
    {
        yield break;
    }

    private async IAsyncEnumerable<byte[]> GetAsyncStream(IEnumerable<byte[]> chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<NpcStreamChunk> GetAiChunks()
    {
        yield return new MetaChunk("Hello", "Happy", 2024);
        yield return new AudioChunk(new byte[] { 0xDE, 0xAD });
        yield return new DoneChunk();
    }
}
