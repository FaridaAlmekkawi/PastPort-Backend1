using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PastPort.Application.Interfaces;

namespace PastPort.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public T? Get<T>(string key)
    {
        return _memoryCache.Get<T>(key);
    }

    public void Set<T>(string key, T value, TimeSpan absoluteExpirationRelativeToNow)
    {
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(absoluteExpirationRelativeToNow)
            .RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
                _logger.LogInformation("Cache key {Key} evicted. Reason: {Reason}", evictedKey, reason));

        _memoryCache.Set(key, value, cacheOptions);
    }

    public void Remove(string key)
    {
        _memoryCache.Remove(key);
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        return _memoryCache.TryGetValue(key, out value);
    }
}
