// ✅ FIXED: AssetRepository.cs
// أضفنا GetAssetByNameAsync — بتعمل WHERE في الـ database مباشرة

using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Domain.Interfaces;

namespace PastPort.Infrastructure.Data.Repositories;

public class AssetRepository : Repository<Asset>, IAssetRepository
{
    public AssetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<Asset>> GetAssetsBySceneIdAsync(Guid sceneId)
    {
        return await _dbSet
            .Where(a => a.SceneId == sceneId && a.Status == AssetStatus.Available)
            .OrderBy(a => a.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Asset?> GetAssetByFileNameAsync(string fileName)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.FileName == fileName);
    }

    public async Task<List<Asset>> GetAssetsByTypeAsync(AssetType type)
    {
        return await _dbSet
            .Where(a => a.Type == type && a.Status == AssetStatus.Available)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> AssetExistsAsync(string fileName, string fileHash)
    {
        return await _dbSet
            .AnyAsync(a => a.FileName == fileName && a.FileHash == fileHash);
    }

    // ✅ FIXED: البحث بالاسم في الـ database مباشرة بدل تحميل كل الـ assets
    // الكود القديم في UnityAssetsController كان:
    //   var assets = await _assetRepository.GetAllAsync();              ← SELECT * FROM Assets (كلهم!)
    //   var asset = assets.FirstOrDefault(a => a.Name.Equals(name));   ← فلترة في C# مش في DB
    //
    // دلوقتي:
    //   SELECT TOP 1 * FROM Assets WHERE LOWER(Name) = LOWER(@name)    ← فلترة في DB مباشرة
    public async Task<Asset?> GetAssetByNameAsync(string name)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Name.ToLower() == name.ToLower());
    }
}