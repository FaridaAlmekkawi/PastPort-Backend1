using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.DTOs.Request;
using PastPort.Application.Interfaces;
using System.Security.Claims;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConversationsController(
    IConversationService conversationService,
    ILogger<ConversationsController> logger)
    : BaseApiController
{
    /// <summary>
    /// Create a conversation with a character
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequestDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var conversation = await conversationService.CreateConversationAsync(userId, request);
            logger.LogInformation("Conversation created for user {UserId} with character {CharacterId}",
                userId, request.CharacterId);

            return CreatedAtAction(nameof(GetConversationHistory),
                new { characterId = request.CharacterId },
                new { data = conversation, message = "Conversation created successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create conversation");
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Get user's conversation history
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserConversations()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var conversations = await conversationService.GetUserConversationsAsync(userId);
            return Ok(new { data = conversations, message = "Conversations retrieved successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve user conversations");
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Get conversation history with specific character
    /// </summary>
    [HttpGet("character/{characterId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationHistory(Guid characterId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var history = await conversationService
                .GetConversationHistoryWithCharacterAsync(userId, characterId);

            return Ok(new { data = history, message = "Conversation history retrieved successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve conversation history for character {CharacterId}",
                characterId);
            return HandleError(ex);
        }
    }
}