// PastPort.API/Controllers/NpcSessionController.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using PastPort.Application.Models.Npc;

namespace PastPort.API.Controllers;

// ── Request / Response DTOs ───────────────────────────────────────────────────

public sealed record StartSessionRequest(
    [Required, MinLength(1)] string YearRange,
    [Required, MinLength(1)] string LocationOldName,
    [Required, MinLength(1)] string Civilization
);

public sealed record StartSessionResponse(
    string SessionId,
    DateTime ExpiresAt);

// ── Controller ────────────────────────────────────────────────────────────────

[Authorize]
[ApiController]
[Route("api/npc")]
public sealed class NpcSessionController(
    ICacheService cache,
    ILogger<NpcSessionController> logger)
    : ControllerBase
{
    // Sessions survive for this long in cache.
    // Must be longer than the longest expected VR scene.
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);

    /// <summary>
    /// Flutter calls this once per scene to establish a session.
    /// Returns a sessionId that Unity passes to the SignalR hub.
    /// </summary>
    [HttpPost("session/start")]
    [ProducesResponseType<StartSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult StartSession([FromBody] StartSessionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var sessionId = Guid.NewGuid().ToString("N"); // compact, no hyphens
        var expiresAt = DateTime.UtcNow.Add(SessionTtl);
        var sessionData = new NpcSessionData(
            YearRange: request.YearRange,
            LocationOldName: request.LocationOldName,
            Civilization: request.Civilization,
            CreatedAt: DateTime.UtcNow);

        cache.Set(BuildCacheKey(sessionId), sessionData, SessionTtl);

        logger.LogInformation(
            "NPC session {SessionId} created for civilization '{Civ}', expires {Exp:O}",
            sessionId, request.Civilization, expiresAt);

        return CreatedAtAction(
            nameof(GetSession),
            new { sessionId },
            new StartSessionResponse(sessionId, expiresAt));
    }

    /// <summary>
    /// Optional: lets Flutter confirm a session is still live.
    /// </summary>
    [HttpGet("session/{sessionId}")]
    [ProducesResponseType<StartSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSession(string sessionId)
    {
        if (cache.TryGetValue(BuildCacheKey(sessionId), out NpcSessionData? data)
            && data is not null)
        {
            return Ok(new
            {
                sessionId,
                civilization = data.Civilization,
                yearRange = data.YearRange,
                locationOldName = data.LocationOldName,
                createdAt = data.CreatedAt
            });
        }

        return NotFound(new { message = $"Session '{sessionId}' not found or expired." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Namespaced cache key — prevents collisions with other cache users.
    /// The hub uses the same method via a shared static helper.
    /// </summary>
    public static string BuildCacheKey(string sessionId)
        => $"npc:session:{sessionId}";
}