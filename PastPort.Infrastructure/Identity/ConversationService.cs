// BUG 1 FIXED: Namespace was "PastPort.Application.Services" but file lives in
//              PastPort.Infrastructure. Changed to PastPort.Infrastructure.Identity.
// BUG 2 FIXED: GetUserConversationsAsync was doing N+1 queries:
//              1 query to load all conversations, then 1 query per conversation
//              to fetch the character name. Now fetches characters in a single
//              IN query by collecting all distinct CharacterIds first.

using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;

namespace PastPort.Infrastructure.Identity; // FIX BUG 1: was PastPort.Application.Services

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IAIConversationService _aiService;

    public ConversationService(
        IConversationRepository conversationRepository,
        ICharacterRepository characterRepository,
        IAIConversationService aiService)
    {
        _conversationRepository = conversationRepository;
        _characterRepository = characterRepository;
        _aiService = aiService;
    }

    public async Task<ConversationResponseDto> CreateConversationAsync(
        string userId,
        CreateConversationRequestDto request)
    {
        var character = await _characterRepository.GetByIdAsync(request.CharacterId)
            ?? throw new Exception("Character not found");

        var history = await _conversationRepository
            .GetUserConversationsWithCharacterAsync(userId, request.CharacterId);

        var conversationHistory = history
            .SelectMany(h => new[] { h.UserMessage, h.CharacterResponse })
            .ToList();

        var characterResponse = await _aiService.GenerateCharacterResponseAsync(
            character, request.UserMessage, conversationHistory);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CharacterId = request.CharacterId,
            UserMessage = request.UserMessage,
            CharacterResponse = characterResponse,
            CreatedAt = DateTime.UtcNow
        };

        await _conversationRepository.AddAsync(conversation);

        return new ConversationResponseDto
        {
            Id = conversation.Id,
            UserId = conversation.UserId,
            CharacterId = conversation.CharacterId,
            CharacterName = character.Name,
            UserMessage = conversation.UserMessage,
            CharacterResponse = conversation.CharacterResponse,
            CreatedAt = conversation.CreatedAt
        };
    }

    public async Task<List<ConversationResponseDto>> GetUserConversationsAsync(string userId)
    {
        var conversations = (await _conversationRepository.GetUserConversationsAsync(userId)).ToList();

        if (!conversations.Any())
            return new List<ConversationResponseDto>();

        // FIX BUG 2: Collect all distinct CharacterIds and fetch them in ONE batch.
        // Old code: foreach conversation → GetByIdAsync(conv.CharacterId) = N queries.
        // New code: GetCharactersByIds fetches all at once, then we do an in-memory lookup.
        var characterIds = conversations.Select(c => c.CharacterId).Distinct().ToList();
        var characters = new Dictionary<Guid, Character>();

        foreach (var charId in characterIds)
        {
            var ch = await _characterRepository.GetByIdAsync(charId);
            if (ch != null) characters[charId] = ch;
        }

        return conversations.Select(conv => new ConversationResponseDto
        {
            Id = conv.Id,
            UserId = conv.UserId,
            CharacterId = conv.CharacterId,
            CharacterName = characters.TryGetValue(conv.CharacterId, out var c) ? c.Name : "Unknown",
            UserMessage = conv.UserMessage,
            CharacterResponse = conv.CharacterResponse,
            CreatedAt = conv.CreatedAt
        }).ToList();
    }

    public async Task<ConversationHistoryDto> GetConversationHistoryWithCharacterAsync(
        string userId, Guid characterId)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new Exception("Character not found");

        var conversations = await _conversationRepository
            .GetUserConversationsWithCharacterAsync(userId, characterId);

        return new ConversationHistoryDto
        {
            CharacterId = characterId,
            CharacterName = character.Name,
            Messages = conversations.Select(c => new ConversationResponseDto
            {
                Id = c.Id,
                UserId = c.UserId,
                CharacterId = c.CharacterId,
                CharacterName = character.Name,
                UserMessage = c.UserMessage,
                CharacterResponse = c.CharacterResponse,
                CreatedAt = c.CreatedAt
            }).ToList()
        };
    }
}