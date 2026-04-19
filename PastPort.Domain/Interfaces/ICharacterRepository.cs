// ✅ FIXED: ICharacterRepository.cs
// أضفنا GetAllWithScenesAsync عشان نحل مشكلة الـ N+1 query
// في CharacterService.GetAllCharactersAsync

using PastPort.Domain.Entities;

namespace PastPort.Domain.Interfaces;

public interface ICharacterRepository : IRepository<Character>
{
    Task<IEnumerable<Character>> GetCharactersBySceneIdAsync(Guid sceneId);

    Task<Character?> GetCharacterWithSceneAsync(Guid characterId);

    // ✅ FIXED: Method جديدة بتجيب كل الـ characters مع الـ scenes بتاعتهم
    // في query واحدة بدل N+1 queries
    Task<List<Character>> GetAllWithScenesAsync();
}