using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PastPort.Application.Interfaces;

namespace PastPort.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache distributedCache, ILogger<CacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public T? Get<T>(string key)
    {
        var data = _distributedCache.GetString(key);
        return data == null ? default : JsonSerializer.Deserialize<T>(data);
    }

    public void Set<T>(string key, T value, TimeSpan absoluteExpirationRelativeToNow)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow
        };
        var data = JsonSerializer.Serialize(value);
        _distributedCache.SetString(key, data, options);
    }

    public void Remove(string key)
    {
        _distributedCache.Remove(key);
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        var data = _distributedCache.GetString(key);
        if (data == null)
        {
            value = default;
            return false;
        }
        value = JsonSerializer.Deserialize<T>(data);
        return true;
    }
}
