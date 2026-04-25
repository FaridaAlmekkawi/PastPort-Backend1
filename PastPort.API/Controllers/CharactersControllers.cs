using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.DTOs.Request;
using PastPort.Application.Interfaces;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CharactersController(
    ICharacterService characterService,
    ILogger<CharactersController> logger)
    : BaseApiController
{
    /// <summary>
    /// Get all characters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCharacters()
    {
        try
        {
            var characters = await characterService.GetAllCharactersAsync();
            return Ok(new { data = characters, message = "Characters retrieved successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve characters");
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Get character by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCharacterById(Guid id)
    {
        try
        {
            var character = await characterService.GetCharacterByIdAsync(id);
            return HandleResult(character);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve character {CharacterId}", id);
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Get characters by scene ID
    /// </summary>
    [HttpGet("scene/{sceneId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCharactersBySceneId(Guid sceneId)
    {
        try
        {
            var characters = await characterService.GetCharactersBySceneIdAsync(sceneId);
            return Ok(new { data = characters, message = "Characters retrieved successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve characters for scene {SceneId}", sceneId);
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Create new character
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCharacter([FromBody] CreateCharacterRequestDto request)
    {
        try
        {
            var character = await characterService.CreateCharacterAsync(request);
            logger.LogInformation("Character created: {CharacterId}", character.Id);
            return CreatedAtAction(nameof(GetCharacterById), new { id = character.Id },
                new { data = character, message = "Character created successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create character");
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Update character
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCharacter(Guid id, [FromBody] UpdateCharacterRequestDto request)
    {
        try
        {
            var character = await characterService.UpdateCharacterAsync(id, request);
            logger.LogInformation("Character updated: {CharacterId}", id);
            return Ok(new { data = character, message = "Character updated successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update character {CharacterId}", id);
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Delete character
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCharacter(Guid id)
    {
        try
        {
            await characterService.DeleteCharacterAsync(id);
            logger.LogInformation("Character deleted: {CharacterId}", id);
            return Ok(new { message = "Character deleted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete character {CharacterId}", id);
            return HandleError(ex);
        }
    }
}