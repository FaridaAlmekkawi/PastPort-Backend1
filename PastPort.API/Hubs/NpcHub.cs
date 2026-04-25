// PastPort.API/Hubs/NpcHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using PastPort.Application.Interfaces;
using PastPort.Application.Models.Npc;
using PastPort.API.Controllers; // for BuildCacheKey

namespace PastPort.API.Hubs;

/// <summary>
/// SignalR hub that Unity connects to.
///
/// Single responsibility: validate the session, delegate to INpcAIService,
/// and relay typed chunks back to the caller.
/// It does NOT contain any WebSocket or JSON parsing logic — that lives
/// entirely in NpcAIService.
/// </summary>
[Authorize]
public sealed class NpcHub : Hub
{
    // Maximum audio payload Unity may send in a single call (10 MB).
    private const int MaxAudioBytes = 10 * 1024 * 1024;

    private readonly INpcAIService _aiService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NpcHub> _logger;

    public NpcHub(
        INpcAIService aiService,
        IMemoryCache cache,
        ILogger<NpcHub> logger)
    {
        _aiService = aiService;
        _cache = cache;
        _logger = logger;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Unity client connected: ConnectionId={Id}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
            _logger.LogWarning(exception,
                "Unity client disconnected with error: ConnectionId={Id}",
                Context.ConnectionId);
        else
            _logger.LogInformation(
                "Unity client disconnected cleanly: ConnectionId={Id}",
                Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }

    // ── Hub Methods (called by Unity) ─────────────────────────────────────────

    /// <summary>
    /// Unity calls this method over SignalR.
    ///
    /// Expected Unity client code (C#):
    ///   await _hubConnection.InvokeAsync(
    ///       "StartConversation", sessionId, roleOrName, audioBytes);
    /// </summary>
    public async Task StartConversation(
        string sessionId,
        string roleOrName,
        byte[] audioBytes)
    {
        // ── Guard: session must exist in cache ─────────────────────────────
        var cacheKey = NpcSessionController.BuildCacheKey(sessionId);
        if (!_cache.TryGetValue(cacheKey, out NpcSessionData? sessionData)
            || sessionData is null)
        {
            _logger.LogWarning(
                "StartConversation called with unknown/expired sessionId='{Id}' " +
                "from connection={Conn}",
                sessionId, Context.ConnectionId);

            await Clients.Caller.SendAsync("OnSessionError",
                "Session not found or expired. Call POST /api/npc/session/start again.");
            return;
        }

        // ── Guard: audio size ──────────────────────────────────────────────
        if (audioBytes is null || audioBytes.Length == 0)
        {
            await Clients.Caller.SendAsync("OnSessionError", "Audio payload is empty.");
            return;
        }

        if (audioBytes.Length > MaxAudioBytes)
        {
            await Clients.Caller.SendAsync("OnSessionError",
                $"Audio payload exceeds the {MaxAudioBytes / 1024 / 1024} MB limit.");
            return;
        }

        _logger.LogInformation(
            "StartConversation: session={Session}, role='{Role}', audio={Bytes} bytes, conn={Conn}",
            sessionId, roleOrName, audioBytes.Length, Context.ConnectionId);

        // ── Stream chunks from the AI service to Unity ─────────────────────
        // Context.ConnectionAborted fires when Unity disconnects mid-stream,
        // propagating into the WebSocket receive loop automatically.
        var ct = Context.ConnectionAborted;

        await foreach (var chunk in _aiService
                           .StreamConversationAsync(audioBytes, sessionData, roleOrName, ct)
                           .WithCancellation(ct))
        {
            // Pattern-match on the sealed record hierarchy
            switch (chunk)
            {
                case MetaChunk meta:
                    await Clients.Caller.SendAsync(
                        "OnMetaReceived",
                        new
                        {
                            text = meta.Text,
                            emotion = meta.Emotion,
                            currentYear = meta.CurrentYear
                        },
                        ct);
                    break;

                case AudioChunk audio:
                    // Send raw bytes — SignalR binary transfer
                    await Clients.Caller.SendAsync(
                        "OnAudioReceived",
                        audio.Bytes,
                        ct);
                    break;

                case DoneChunk:
                    await Clients.Caller.SendAsync("OnConversationDone", ct);
                    _logger.LogInformation(
                        "Conversation complete for session={Session}", sessionId);
                    break;

                case ErrorChunk err:
                    _logger.LogWarning(
                        "LLM error for session={Session}: {Reason}", sessionId, err.Reason);
                    await Clients.Caller.SendAsync("OnSessionError", err.Reason, ct);
                    break;
            }
        }
    }

    /// <summary>
    /// Unity may call this to end a session early (e.g. player exits scene).
    /// </summary>
    public Task EndSession(string sessionId)
    {
        var cacheKey = NpcSessionController.BuildCacheKey(sessionId);
        _cache.Remove(cacheKey);

        _logger.LogInformation(
            "Session {SessionId} ended by client {Conn}", sessionId, Context.ConnectionId);

        return Task.CompletedTask;
    }
}