using System;
using FluentAssertions;
using PastPort.Application.Models.Npc;
using Xunit;

namespace PastPort.Tests.Models;

public sealed class NpcStreamChunkTests
{
    // ── NpcSessionData ─────────────────────────────────────────────────────────

    [Fact]
    public void NpcSessionData_Properties_AreAccessible()
    {
        var now = DateTime.UtcNow;
        var data = new NpcSessionData("500–600 AD", "Alexandria", "Byzantine", now);

        data.YearRange.Should().Be("500–600 AD");
        data.LocationOldName.Should().Be("Alexandria");
        data.Civilization.Should().Be("Byzantine");
        data.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void NpcSessionData_RecordEquality_WorksByValue()
    {
        var now = DateTime.UtcNow;
        var a = new NpcSessionData("Y", "L", "C", now);
        var b = new NpcSessionData("Y", "L", "C", now);

        a.Should().Be(b);
    }

    [Fact]
    public void NpcSessionData_WithExpression_CreatesNewInstance()
    {
        var original = new NpcSessionData("Y", "L", "C", DateTime.UtcNow);
        var modified = original with { Civilization = "Roman" };

        modified.Civilization.Should().Be("Roman");
        original.Civilization.Should().Be("C"); // immutable
    }

    // ── MetaChunk ──────────────────────────────────────────────────────────────

    [Fact]
    public void MetaChunk_Properties_AreAccessible()
    {
        var chunk = new MetaChunk("Hello!", "happy", 1500);

        chunk.Text.Should().Be("Hello!");
        chunk.Emotion.Should().Be("happy");
        chunk.CurrentYear.Should().Be(1500);
    }

    [Fact]
    public void MetaChunk_IsNpcStreamChunk()
    {
        NpcStreamChunk chunk = new MetaChunk("t", "e", 100);

        chunk.Should().BeOfType<MetaChunk>();
    }

    [Fact]
    public void MetaChunk_RecordEquality_WorksByValue()
    {
        var a = new MetaChunk("t", "e", 100);
        var b = new MetaChunk("t", "e", 100);

        a.Should().Be(b);
    }

    // ── AudioChunk ────────────────────────────────────────────────────────────

    [Fact]
    public void AudioChunk_Properties_AreAccessible()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var chunk = new AudioChunk(bytes);

        chunk.Bytes.Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void AudioChunk_IsNpcStreamChunk()
    {
        NpcStreamChunk chunk = new AudioChunk(Array.Empty<byte>());

        chunk.Should().BeOfType<AudioChunk>();
    }

    // ── DoneChunk ─────────────────────────────────────────────────────────────

    [Fact]
    public void DoneChunk_CanBeConstructed()
    {
        var chunk = new DoneChunk();

        chunk.Should().NotBeNull();
    }

    [Fact]
    public void DoneChunk_IsNpcStreamChunk()
    {
        NpcStreamChunk chunk = new DoneChunk();

        chunk.Should().BeOfType<DoneChunk>();
    }

    [Fact]
    public void DoneChunk_RecordEquality_WorksByValue()
    {
        var a = new DoneChunk();
        var b = new DoneChunk();

        a.Should().Be(b);
    }

    // ── ErrorChunk ────────────────────────────────────────────────────────────

    [Fact]
    public void ErrorChunk_Properties_AreAccessible()
    {
        var chunk = new ErrorChunk("Something went wrong");

        chunk.Reason.Should().Be("Something went wrong");
    }

    [Fact]
    public void ErrorChunk_IsNpcStreamChunk()
    {
        NpcStreamChunk chunk = new ErrorChunk("err");

        chunk.Should().BeOfType<ErrorChunk>();
    }

    // ── Pattern matching exhaustiveness ───────────────────────────────────────

    [Theory]
    [InlineData("meta")]
    [InlineData("audio")]
    [InlineData("done")]
    [InlineData("error")]
    public void PatternMatch_HandlesAllChunkTypes(string kind)
    {
        NpcStreamChunk chunk = kind switch
        {
            "meta"  => new MetaChunk("t", "e", 1),
            "audio" => new AudioChunk(new byte[] { 0 }),
            "done"  => new DoneChunk(),
            "error" => new ErrorChunk("r"),
            _       => throw new InvalidOperationException()
        };

        var handled = false;
        switch (chunk)
        {
            case MetaChunk:   handled = true; break;
            case AudioChunk:  handled = true; break;
            case DoneChunk:   handled = true; break;
            case ErrorChunk:  handled = true; break;
        }

        handled.Should().BeTrue();
    }
}
