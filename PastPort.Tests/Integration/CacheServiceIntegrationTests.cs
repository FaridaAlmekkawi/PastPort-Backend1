using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PastPort.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace PastPort.Tests.Integration;

public sealed class CacheServiceIntegrationTests
{
    private readonly CacheService _sut;
    private readonly IMemoryCache _memoryCache;

    public CacheServiceIntegrationTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new CacheService(_memoryCache, NullLogger<CacheService>.Instance);
    }

    [Fact]
    public async Task CacheService_HandlesExpiration_Correctly()
    {
        // Arrange
        var key = "expire-key";
        var value = "expire-value";
        var ttl = TimeSpan.FromMilliseconds(100);

        // Act
        _sut.Set(key, value, ttl);
        _sut.TryGetValue(key, out string? found).Should().BeTrue();
        found.Should().Be(value);

        // Wait for expiration
        await Task.Delay(200);

        // Assert
        _sut.TryGetValue(key, out string? expired).Should().BeFalse();
        expired.Should().BeNull();
    }

    [Fact]
    public void CacheService_Remove_ClearsValue()
    {
        // Arrange
        var key = "remove-key";
        _sut.Set(key, "val", TimeSpan.FromMinutes(1));

        // Act
        _sut.Remove(key);

        // Assert
        _sut.TryGetValue(key, out string? _).Should().BeFalse();
    }
}
