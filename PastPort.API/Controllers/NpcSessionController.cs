// PastPort.API/Controllers/NpcSessionController.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
    DateTime ExpiresAt
);

// ── Controller ────────────────────────────────────────────────────────────────

[Authorize]
[ApiController]
[Route("api/npc")]
public sealed class NpcSessionController : ControllerBase
{
    // Sessions survive for this long in cache.
    // Must be longer than the longest expected VR scene.
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);

    private readonly IMemoryCache _cache;
    private readonly ILogger<NpcSessionController> _logger;

    public NpcSessionController(
        IMemoryCache cache,
        ILogger<NpcSessionController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

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

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(SessionTtl)
            // Eviction callback so we can log and clean up if needed
            .RegisterPostEvictionCallback((key, _, reason, _) =>
                _logger.LogInformation(
                    "NPC session {Key} evicted. Reason: {Reason}", key, reason));

        _cache.Set(BuildCacheKey(sessionId), sessionData, cacheOptions);

        _logger.LogInformation(
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
        if (_cache.TryGetValue(BuildCacheKey(sessionId), out NpcSessionData? data)
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