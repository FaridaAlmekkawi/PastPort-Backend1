using System;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PastPort.Application.Models.Npc;
using PastPort.Infrastructure.ExternalServices.AI;
using Xunit;

namespace PastPort.Tests.Services;

public class NpcAIServiceTests
{
    private readonly NpcAIService _sut;

    public NpcAIServiceTests()
    {
        var settings = Options.Create(new NpcAISettings
        {
            WebSocketUrl = "ws://test"
        });
        _sut = new NpcAIService(settings, NullLogger<NpcAIService>.Instance);
    }

    [Fact]
    public void ParseBinaryFrame_ReturnsAudioChunk()
    {
        var data = new byte[] { 1, 2, 3 };
        
        var result = NpcAIService.ParseBinaryFrame(data);

        result.Should().BeOfType<AudioChunk>()
            .Which.Bytes.Should().Equal(data);
    }

    [Fact]
    public void ParseTextFrame_WithMeta_ReturnsMetaChunk()
    {
        var json = "{\"type\": \"meta\", \"text\": \"Hello\", \"emotion\": \"Happy\", \"year\": 2024}";
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        var meta = result.Should().BeOfType<MetaChunk>().Subject;
        meta.Text.Should().Be("Hello");
        meta.Emotion.Should().Be("Happy");
        meta.CurrentYear.Should().Be(2024);
    }

    [Fact]
    public void ParseTextFrame_WithLegacyCurrentYear_ReturnsMetaChunk()
    {
        var json = "{\"type\": \"meta\", \"text\": \"Hello\", \"emotion\": \"Happy\", \"current_year\": 2024}";
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        result.Should().BeOfType<MetaChunk>()
            .Which.CurrentYear.Should().Be(2024);
    }

    [Fact]
    public void ParseTextFrame_WithError_ReturnsErrorChunk()
    {
        var json = "{\"type\": \"error\", \"reason\": \"transcription_failed\"}";
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        result.Should().BeOfType<ErrorChunk>()
            .Which.Reason.Should().Be("transcription_failed");
    }

    [Fact]
    public void ParseTextFrame_WithDone_ReturnsDoneChunk()
    {
        var json = "{\"type\": \"done\"}";
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        result.Should().BeOfType<DoneChunk>();
    }

    [Fact]
    public void ParseTextFrame_WithUnknownType_ReturnsErrorChunk()
    {
        var json = "{\"type\": \"unknown\"}";
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        result.Should().BeOfType<ErrorChunk>()
            .Which.Reason.Should().Contain("Unknown LLM frame type");
    }

    [Fact]
    public void ParseTextFrame_WithMalformedJson_ReturnsErrorChunk()
    {
        var json = "{\"type\": \"meta\", \"text\": \"Hello\""; // missing bracket
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        result.Should().BeOfType<ErrorChunk>()
            .Which.Reason.Should().Contain("Malformed JSON");
    }

    [Fact]
    public void ParseTextFrame_WithNullFrame_ReturnsErrorChunk()
    {
        var json = "null";
        var data = Encoding.UTF8.GetBytes(json);

        var result = _sut.ParseTextFrame(data);

        result.Should().BeOfType<ErrorChunk>()
            .Which.Reason.Should().Contain("Received null JSON frame");
    }

    [Theory]
    [InlineData("300 BC - 30 BC", 300, 30)]
    [InlineData("2550, 2580", 2550, 2580)]
    [InlineData("1900", 1900, 1900)]
    public void ParseYearRange_ExtractsEngineRange(string input, int start, int end)
    {
        var result = NpcAIService.ParseYearRange(input);

        result.Should().Equal(start, end);
    }

    [Theory]
    [InlineData("wss://gizmo-battering-moaning.ngrok-free.dev", "wss://gizmo-battering-moaning.ngrok-free.dev/ws/npc")]
    [InlineData("wss://gizmo-battering-moaning.ngrok-free.dev/", "wss://gizmo-battering-moaning.ngrok-free.dev/ws/npc")]
    [InlineData("wss://gizmo-battering-moaning.ngrok-free.dev/ws/npc", "wss://gizmo-battering-moaning.ngrok-free.dev/ws/npc")]
    public void NormalizeWebSocketUrl_UsesNpcEndpoint(string input, string expected)
    {
        var result = NpcAIService.NormalizeWebSocketUrl(input);

        result.Should().Be(expected);
    }
}
