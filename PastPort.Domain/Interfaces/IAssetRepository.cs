// ✅ FIXED: IAssetRepository.cs
// أضفنا GetAssetByNameAsync عشان UnityAssetsController.SearchAsset
// يعمل WHERE في الـ database بدل ما يحمّل كل الـ assets في الـ memory

using PastPort.Domain.Entities;
using PastPort.Domain.Enums;

namespace PastPort.Domain.Interfaces;

public interface IAssetRepository : IRepository<Asset>
{
    Task<List<Asset>> GetAssetsBySceneIdAsync(Guid sceneId);

    Task<Asset?> GetAssetByFileNameAsync(string fileName);

    Task<List<Asset>> GetAssetsByTypeAsync(AssetType type);

    Task<bool> AssetExistsAsync(string fileName, string fileHash);

    // ✅ FIXED: Method جديدة للبحث بالاسم في الـ database مباشرة
    // بدل GetAllAsync() + FirstOrDefault() اللي بتحمّل كل الـ assets في الـ memory
    Task<Asset?> GetAssetByNameAsync(string name);
}