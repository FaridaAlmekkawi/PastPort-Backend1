namespace PastPort.Application.Interfaces;

/// <summary>
/// Abstraction over the caching infrastructure, providing a simple
/// key-value store for session data and frequently accessed objects.
/// The default implementation wraps <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>,
/// but can be swapped for <c>IDistributedCache</c> (Redis) for multi-instance deployments.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Stores a value in the cache with the specified key and time-to-live.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key (e.g., <c>"npc:session:{id}"</c>).</param>
    /// <param name="value">The value to store.</param>
    /// <param name="absoluteExpiration">The duration after which the entry is evicted.</param>
    void Set<T>(string key, T value, TimeSpan absoluteExpiration);

    /// <summary>
    /// Attempts to retrieve a value from the cache.
    /// </summary>
    /// <typeparam name="T">The expected type of the cached value.</typeparam>
    /// <param name="key">The cache key to look up.</param>
    /// <param name="value">When this method returns, contains the cached value if found; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    bool TryGetValue<T>(string key, out T? value);

    /// <summary>
    /// Removes an entry from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    void Remove(string key);
}
