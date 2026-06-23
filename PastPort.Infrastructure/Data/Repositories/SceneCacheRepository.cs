using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;

namespace PastPort.Infrastructure.Data.Repositories;

public class SceneCacheRepository : Repository<SceneCache>, ISceneCacheRepository
{
    public SceneCacheRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SceneCache?> GetByCacheKeyAsync(string cacheKey)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.CacheKey == cacheKey &&
                s.ExpiresAt > DateTime.UtcNow);
    }

    public async Task DeleteExpiredAsync()
    {
        var expired = await _dbSet
            .Where(s => s.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        _dbSet.RemoveRange(expired);
        await _context.SaveChangesAsync();
    }
}