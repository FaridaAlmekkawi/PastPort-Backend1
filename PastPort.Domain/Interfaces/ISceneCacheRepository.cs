using PastPort.Domain.Entities;

namespace PastPort.Domain.Interfaces;

public interface ISceneCacheRepository : IRepository<SceneCache>
{
    Task<SceneCache?> GetByCacheKeyAsync(string cacheKey);
    Task DeleteExpiredAsync();
}