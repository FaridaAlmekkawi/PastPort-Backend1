using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace PastPort.Tests.Services;

public sealed class InMemoryDistributedCache : IDistributedCache
{
    private readonly ConcurrentDictionary<string, (byte[] Value, DateTimeOffset? Expiry)> _store = new();

    public byte[]? Get(string key)
    {
        if (!_store.TryGetValue(key, out var entry))
            return null;
        if (entry.Expiry.HasValue && entry.Expiry.Value < DateTimeOffset.UtcNow)
        {
            _store.TryRemove(key, out _);
            return null;
        }
        return entry.Value;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        DateTimeOffset? expiry = options.AbsoluteExpirationRelativeToNow.HasValue
            ? DateTimeOffset.UtcNow + options.AbsoluteExpirationRelativeToNow.Value
            : options.AbsoluteExpiration.HasValue
                ? options.AbsoluteExpiration.Value
                : null;
        _store[key] = (value, expiry);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key) { }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key)
        => _store.TryRemove(key, out _);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
