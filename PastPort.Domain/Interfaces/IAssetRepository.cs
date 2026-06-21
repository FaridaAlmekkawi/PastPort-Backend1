using PastPort.Domain.Enums;
using PastPort.Domain.Interfaces;

public interface IAssetRepository : IRepository<Asset>
{
    Task<List<Asset>> GetAssetsBySceneIdAsync(Guid sceneId);
    Task<Asset?> GetAssetByFileNameAsync(string fileName);
    Task<List<Asset>> GetAssetsByTypeAsync(AssetType type);
    Task<bool> AssetExistsAsync(string fileName, string fileHash);
    Task<Asset?> GetAssetByNameAsync(string name);

    // ✅ جديد: البحث عن asset متخزن بنفس الـ prompt hash (caching)
    Task<Asset?> GetAssetByPromptHashAsync(string promptHash);
}