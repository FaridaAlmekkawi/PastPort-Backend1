using System;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PastPort.Infrastructure.Services;
using Xunit;

namespace PastPort.Tests.Services;

public sealed class CacheServiceTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheService _sut;

    public CacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new CacheService(_memoryCache, NullLogger<CacheService>.Instance);
    }

    public void Dispose() => _memoryCache.Dispose();

    // ── Get ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_WhenKeyExists_ReturnsValue()
    {
        _sut.Set("k", "hello", TimeSpan.FromMinutes(1));

        var result = _sut.Get<string>("k");

        result.Should().Be("hello");
    }

    [Fact]
    public void Get_WhenKeyMissing_ReturnsDefault()
    {
        var result = _sut.Get<string>("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenKeyMissing_ReturnsDefaultInt()
    {
        var result = _sut.Get<int>("nonexistent-int");

        result.Should().Be(0);
    }

    // ── Set ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Set_OverwritesExistingKey()
    {
        _sut.Set("k", "first", TimeSpan.FromMinutes(1));
        _sut.Set("k", "second", TimeSpan.FromMinutes(1));

        _sut.Get<string>("k").Should().Be("second");
    }

    [Fact]
    public void Set_StoresComplexObject()
    {
        var obj = new TestRecord(42, "PastPort");
        _sut.Set("rec", obj, TimeSpan.FromMinutes(1));

        _sut.Get<TestRecord>("rec").Should().BeEquivalentTo(obj);
    }

    [Fact]
    public void Set_WithVeryShortTtl_EvictsEntry()
    {
        _sut.Set("short", "value", TimeSpan.FromMilliseconds(50));

        // Give the cache time to evict
        Thread.Sleep(200);

        // Force expiry check by touching cache
        _sut.Get<string>("short").Should().BeNull();
    }

    // ── Remove ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_WhenKeyExists_KeyNoLongerReturnsValue()
    {
        _sut.Set("k", "v", TimeSpan.FromMinutes(1));

        _sut.Remove("k");

        _sut.Get<string>("k").Should().BeNull();
    }

    [Fact]
    public void Remove_WhenKeyDoesNotExist_DoesNotThrow()
    {
        var act = () => _sut.Remove("nonexistent");

        act.Should().NotThrow();
    }

    [Fact]
    public void Remove_OnlyRemovesTargetKey()
    {
        _sut.Set("a", "1", TimeSpan.FromMinutes(1));
        _sut.Set("b", "2", TimeSpan.FromMinutes(1));

        _sut.Remove("a");

        _sut.Get<string>("a").Should().BeNull();
        _sut.Get<string>("b").Should().Be("2");
    }

    // ── TryGetValue ────────────────────────────────────────────────────────────

    [Fact]
    public void TryGetValue_WhenKeyExists_ReturnsTrueAndValue()
    {
        _sut.Set("k", 99, TimeSpan.FromMinutes(1));

        var found = _sut.TryGetValue<int>("k", out var value);

        found.Should().BeTrue();
        value.Should().Be(99);
    }

    [Fact]
    public void TryGetValue_WhenKeyMissing_ReturnsFalse()
    {
        var found = _sut.TryGetValue<int>("missing", out var value);

        found.Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact]
    public void TryGetValue_AfterRemove_ReturnsFalse()
    {
        _sut.Set("k", "v", TimeSpan.FromMinutes(1));
        _sut.Remove("k");

        var found = _sut.TryGetValue<string>("k", out var value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    // ── Multiple types ─────────────────────────────────────────────────────────

    [Fact]
    public void Set_DifferentKeys_StoreIndependently()
    {
        _sut.Set("int-key", 1, TimeSpan.FromMinutes(1));
        _sut.Set("str-key", "hello", TimeSpan.FromMinutes(1));

        _sut.Get<int>("int-key").Should().Be(1);
        _sut.Get<string>("str-key").Should().Be("hello");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private sealed record TestRecord(int Id, string Name);
}
