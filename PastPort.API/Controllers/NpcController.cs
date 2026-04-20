// ============================================================
//  NpcController.cs — PastPort.API/Controllers
//
//  GAP 11 FIX: Audio null check was AFTER session null check.
//  If both are missing, user got "Session not found" (404) instead
//  of "Audio is required" (400) — wrong error, confuses the client.
//  Audio validation now comes first since it's a client-side error.
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace PastPort.API.Controllers;

[Authorize]
public class NpcController : BaseApiController
{
    private readonly INpcAIService _npcAIService;
    private readonly INpcSessionStore _sessionStore;
    private readonly ILogger<NpcController> _logger;

    public NpcController(
        INpcAIService npcAIService,
        INpcSessionStore sessionStore,
        ILogger<NpcController> logger)
    {
        _npcAIService = npcAIService;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <summary>
    /// Flutter calls this first to register world context and receive a sessionId.
    /// POST /api/npc/session/start
    /// Body: { "yearRange": "...", "locationOldName": "...", "civilization": "..." }
    /// </summary>
    [HttpPost("session/start")]
    public IActionResult StartSession([FromBody] StartSessionRequest request)
    {
        var sessionData = new NpcSessionData
        {
            YearRange = request.YearRange,
            LocationOldName = request.LocationOldName,
            Civilization = request.Civilization
        };

        var sessionId = _sessionStore.CreateSession(sessionData);

        _logger.LogInformation(
            "NPC session created. SessionId={SessionId} Civilization={Civ}",
            sessionId, request.Civilization);

        return Ok(new { sessionId });
    }

    /// <summary>
    /// Unity calls this to send voice audio and receive a streamed NPC response.
    /// POST /api/npc/stream   (multipart/form-data)
    /// Form fields: audio (file), sessionId (string), roleOrName (string)
    ///
    /// FIX: Audio is validated FIRST — it's a 400 client error.
    ///      Session is validated SECOND — it's a 404 not-found error.
    ///      Old code had them reversed, producing a confusing 404 when
    ///      the real problem was a missing audio field.
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream(
        [FromForm] IFormFile audio,
        [FromForm] string sessionId,
        [FromForm] string roleOrName,
        CancellationToken cancellationToken)
    {
        // FIX GAP 11: Validate audio FIRST (400 Bad Request)
        if (audio == null || audio.Length == 0)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("Audio is required");
            return;
        }

        // Then validate session (404 Not Found)
        var sessionData = _sessionStore.GetSession(sessionId);
        if (sessionData == null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsync("Session not found or expired. Call /session/start first.");
            return;
        }

        var world = new NpcWorldDto
        {
            YearRange = sessionData.YearRange,
            LocationOldName = sessionData.LocationOldName,
            Civilization = sessionData.Civilization,
            RoleOrName = roleOrName
        };

        _logger.LogInformation(
            "NPC stream. Session={SessionId} Role={Role} Civ={Civ}",
            sessionId, roleOrName, world.Civilization);

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms, cancellationToken);
        var audioBytes = ms.ToArray();

        Response.ContentType = "application/x-ndjson";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var chunk in _npcAIService.SendAudioAndGetResponseAsync(
            audioBytes, world, sessionId, cancellationToken))
        {
            var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            await Response.WriteAsync(json + "\n", Encoding.UTF8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Ends the session and frees in-memory state.
    /// POST /api/npc/session/end
    /// </summary>
    [HttpPost("session/end")]
    public IActionResult EndSession([FromBody] EndSessionRequest request)
    {
        _sessionStore.RemoveSession(request.SessionId);
        _logger.LogInformation("NPC session ended. SessionId={SessionId}", request.SessionId);
        return Ok(new { message = "Session ended" });
    }
}

public class StartSessionRequest
{
    public string YearRange { get; set; } = string.Empty;
    public string LocationOldName { get; set; } = string.Empty;
    public string Civilization { get; set; } = string.Empty;
}

public class EndSessionRequest
{
    public string SessionId { get; set; } = string.Empty;
}