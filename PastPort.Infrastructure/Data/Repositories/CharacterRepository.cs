// ✅ FIXED: CharacterRepository.cs
// أضفنا GetAllWithScenesAsync اللي بتعمل single JOIN query بدل N+1

using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;

namespace PastPort.Infrastructure.Data.Repositories;

public class CharacterRepository : Repository<Character>, ICharacterRepository
{
    public CharacterRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Character>> GetCharactersBySceneIdAsync(Guid sceneId)
    {
        return await _dbSet
            .Where(c => c.SceneId == sceneId)
            .Include(c => c.Scene)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Character?> GetCharacterWithSceneAsync(Guid characterId)
    {
        return await _dbSet
            .Include(c => c.Scene)
            .FirstOrDefaultAsync(c => c.Id == characterId);
    }

    // ✅ FIXED: بدل الكود القديم اللي كان:
    //    var characters = await GetAllAsync();           // query 1: SELECT * FROM Characters
    //    foreach (var c in characters)                   // N queries داخل الـ loop
    //        await GetCharacterWithSceneAsync(c.Id);     // SELECT * FROM Characters JOIN Scenes WHERE Id = ?
    //
    // دلوقتي: query واحدة بس بتجيب كل الـ characters مع الـ scenes بتاعتهم
    //    SELECT * FROM Characters c
    //    LEFT JOIN HistoricalScenes s ON c.SceneId = s.Id
    //
    // AsNoTracking() لأن ده read-only operation — بيوفر memory وسرعة
    public async Task<List<Character>> GetAllWithScenesAsync()
    {
        return await _dbSet
            .Include(c => c.Scene)
            .AsNoTracking()
            .ToListAsync();
    }
}