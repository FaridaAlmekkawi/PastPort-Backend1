// FIX 1: Namespace changed to Application layer (Clean Architecture)
// FIX 2: Resolved N+1 query by using the new repo method GetUserConversationsWithCharactersAsync
// FIX 3: Structured history passed to AI with explicit roles (User vs Character Name)
// FIX 4: Added try-catch and null check to handle DB persist failures gracefully.

using Mapster;
using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;

namespace PastPort.Application.Services; // ✅ FIX 1: Correct namespace for Application Layer

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

        // ✅ FIX 3: Pass structured history to AI with roles so it understands context
        var conversationHistory = history
            .SelectMany(h => new[]
            {
                $"User: {h.UserMessage}",
                $"{character.Name}: {h.CharacterResponse}"
            })
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

        // ✅ FIX 4: Meaningful exception handling if DB save fails
        try
        {
            await _conversationRepository.AddAsync(conversation);
        }
        catch (Exception ex)
        {
            // Propagate with a meaningful message instead of a silent failure
            throw new Exception("Failed to persist the conversation to the database.", ex);
        }

        var response = conversation.Adapt<ConversationResponseDto>();
        response.CharacterName = character.Name;
        return response;
    }

    public async Task<List<ConversationResponseDto>> GetUserConversationsAsync(string userId)
    {
        // ✅ FIX 2: Fetch all conversations with characters in ONE query (resolves N+1 issue)
        var conversations = await _conversationRepository.GetUserConversationsWithCharactersAsync(userId);

        if (conversations == null || !conversations.Any())
            return new List<ConversationResponseDto>();

        return conversations.Select(conv => 
        {
            var dto = conv.Adapt<ConversationResponseDto>();
            dto.CharacterName = conv.Character?.Name ?? "Unknown";
            return dto;
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
            Messages = conversations.Select(c => 
            {
                var dto = c.Adapt<ConversationResponseDto>();
                dto.CharacterName = character.Name;
                return dto;
            }).ToList()
        };
    }
}