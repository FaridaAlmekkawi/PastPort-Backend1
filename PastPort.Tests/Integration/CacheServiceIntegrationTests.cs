using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PastPort.Infrastructure.Services;
using PastPort.Tests.Services;
using Xunit;

namespace PastPort.Tests.Integration;

public sealed class CacheServiceIntegrationTests
{
    private readonly CacheService _sut;

    public CacheServiceIntegrationTests()
    {
        var cache = new InMemoryDistributedCache();
        _sut = new CacheService(cache, NullLogger<CacheService>.Instance);
    }

    [Fact]
    public async Task CacheService_HandlesExpiration_Correctly()
    {
        var key = "expire-key";
        var value = "expire-value";
        var ttl = TimeSpan.FromMilliseconds(50);

        _sut.Set(key, value, ttl);
        _sut.TryGetValue(key, out string? found).Should().BeTrue();
        found.Should().Be(value);

        await Task.Delay(100);

        _sut.TryGetValue(key, out string? expired).Should().BeFalse();
        expired.Should().BeNull();
    }

    [Fact]
    public void CacheService_Remove_ClearsValue()
    {
        var key = "remove-key";
        _sut.Set(key, "val", TimeSpan.FromMinutes(1));
        _sut.Remove(key);
        _sut.TryGetValue(key, out string? _).Should().BeFalse();
    }

    [Fact]
    public void CacheService_StoreAndRetrieve_ComplexObject()
    {
        var key = "complex-key";
        var obj = new TestData { Id = 1, Name = "Test" };

        _sut.Set(key, obj, TimeSpan.FromMinutes(1));
        var result = _sut.Get<TestData>(key);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    private sealed class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
