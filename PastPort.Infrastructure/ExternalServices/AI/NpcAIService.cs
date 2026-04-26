using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PastPort.Application.Interfaces;
using PastPort.Application.Models.Npc;

namespace PastPort.Infrastructure.ExternalServices.AI;

// ── Configuration ────────────────────────────────────────────────────────────

public sealed class NpcAISettings
{
    /// <summary>e.g. "ws://python-api.internal/ws/npc"</summary>
    public string WebSocketUrl { get; init; } = string.Empty;

    /// <summary>
    /// Receive buffer in bytes.
    /// 64 KB covers most meta JSON; audio chunks can be larger — the
    /// reader reassembles fragmented frames automatically.
    /// </summary>
    public int ReceiveBufferBytes { get; init; } = 65_536;

    /// <summary>Max time the whole WebSocket conversation may run.</summary>
    public int ConversationTimeoutSeconds { get; init; } = 120;
}

// ── Private DTOs (send / receive) ─────────────────────────────────────────────

file sealed class LlmSendPayload
{
    [JsonPropertyName("audio")] public int[] Audio { get; init; } = [];
    [JsonPropertyName("world")] public LlmWorld World { get; init; } = new();
}

file sealed class LlmWorld
{
    [JsonPropertyName("year_range")] public string YearRange { get; init; } = "";
    [JsonPropertyName("location_old_name")] public string LocationOldName { get; init; } = "";
    [JsonPropertyName("civilization")] public string Civilization { get; init; } = "";
    [JsonPropertyName("role_or_name")] public string RoleOrName { get; init; } = "";
}

file sealed class LlmTextFrame
{
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("emotion")] public string? Emotion { get; init; }
    [JsonPropertyName("current_year")] public int CurrentYear { get; init; }
}

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class NpcAIService : INpcAIService
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly NpcAISettings _settings;
    private readonly ILogger<NpcAIService> _logger;

    public NpcAIService(
        IOptions<NpcAISettings> settings,
        ILogger<NpcAIService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.WebSocketUrl))
            throw new InvalidOperationException(
                "NpcAI:WebSocketUrl is not configured.");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<NpcStreamChunk> StreamConversationAsync(
        byte[] audioBytes,
        NpcSessionData sessionData,
        string roleOrName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(
            TimeSpan.FromSeconds(_settings.ConversationTimeoutSeconds));

        var token = timeoutCts.Token;
        using var ws = new ClientWebSocket();

        // ── 1. Connect ──────────────────────────────────────────────────────────
        // FIX CS1631: capture error outside catch, yield after the try/catch block
        NpcStreamChunk? earlyError = null;

        try
        {
            await ws.ConnectAsync(new Uri(_settings.WebSocketUrl), token);
            _logger.LogInformation(
                "NPC WS connected to {Url} for role '{Role}'",
                _settings.WebSocketUrl, roleOrName);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            _logger.LogError(ex, "NPC WebSocket connection failed");
            earlyError = new ErrorChunk("Could not connect to the LLM service.");
        }

        if (earlyError is not null)
        {
            yield return earlyError;
            yield break;
        }

        // ── 2. Send payload ──────────────────────────────────────────────────────
        try
        {
            var payload = new LlmSendPayload
            {
                Audio = audioBytes.Select(b => (int)(sbyte)b).ToArray(),
                World = new LlmWorld
                {
                    YearRange = sessionData.YearRange,
                    LocationOldName = sessionData.LocationOldName,
                    Civilization = sessionData.Civilization,
                    RoleOrName = roleOrName
                }
            };

            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            var encoded = Encoding.UTF8.GetBytes(json);

            await ws.SendAsync(
                encoded.AsMemory(),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: token);

            _logger.LogDebug(
                "NPC WS sent payload: audio={Bytes} bytes", audioBytes.Length);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to send NPC WS payload");
            // FIX CS1631: set local, yield after catch
            earlyError = new ErrorChunk("Failed to send audio to the LLM.");
        }

        if (earlyError is not null)
        {
            await CloseWebSocketAsync(ws);
            yield return earlyError;
            yield break;
        }

        // ── 3. Stream responses ──────────────────────────────────────────────────
        var buffer = new byte[_settings.ReceiveBufferBytes];
        using var accumulator = new MemoryStream();

        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            // ✅ تم حل الخطأ CS0165 عن طريق إعطاء قيمة مبدئية null!
            WebSocketReceiveResult result = null!;
            NpcStreamChunk? receiveError = null;

            try
            {
                result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("NPC WS receive cancelled");
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "NPC WS receive error");
                receiveError = new ErrorChunk("LLM connection dropped unexpectedly.");
            }

            if (receiveError is not null)
            {
                yield return receiveError;
                yield break;
            }

            if (result!.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("NPC WS closed by server");
                break;
            }

            accumulator.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage)
                continue;

            var fullMessage = accumulator.ToArray();
            accumulator.SetLength(0);

            var chunk = result.MessageType switch
            {
                WebSocketMessageType.Binary => ParseBinaryFrame(fullMessage),
                WebSocketMessageType.Text => ParseTextFrame(fullMessage),
                _ => null
            };

            if (chunk is DoneChunk)
            {
                yield return chunk;
                break;
            }

            if (chunk is not null)
                yield return chunk;
        }

        await CloseWebSocketAsync(ws);
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    internal static NpcStreamChunk ParseBinaryFrame(byte[] data)
        => new AudioChunk(data);

    internal NpcStreamChunk ParseTextFrame(byte[] data)
    {
        try
        {
            var frame = JsonSerializer.Deserialize<LlmTextFrame>(data, _jsonOpts);

            if (frame is null)
                return new ErrorChunk("Received null JSON frame from LLM.");

            return frame.Type switch
            {
                "meta" => new MetaChunk(
                    Text: frame.Text ?? string.Empty,
                    Emotion: frame.Emotion ?? string.Empty,
                    CurrentYear: frame.CurrentYear),

                "done" => new DoneChunk(),

                _ => new ErrorChunk($"Unknown LLM frame type: '{frame.Type}'")
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM text frame");
            return new ErrorChunk("Malformed JSON from LLM.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task CloseWebSocketAsync(ClientWebSocket ws)
    {
        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Conversation complete",
                    closeCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NPC WS close handshake failed (non-critical)");
            }
        }
    }
}