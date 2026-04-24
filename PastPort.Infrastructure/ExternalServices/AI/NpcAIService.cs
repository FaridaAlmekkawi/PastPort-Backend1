// ============================================================
//  NpcAIService.cs — PastPort.Infrastructure/ExternalServices/AI
//
//  GAP 18 FIX: Fail fast if BaseUrl is missing.
//  FIX 4: Removed double-serialization of 'world' object. Passed 
//  parameters directly as MultipartFormData fields to prevent 
//  fragile parsing issues on the AI backend.
// ============================================================
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PastPort.Application.Interfaces;

namespace PastPort.Infrastructure.ExternalServices.AI;

public class NpcAISettings
{
    public string BaseUrl { get; set; } = string.Empty;
}

public class NpcAIService : INpcAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NpcAIService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public NpcAIService(
        HttpClient httpClient,
        IOptions<NpcAISettings> settings,
        ILogger<NpcAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // FIX GAP 18: Validate BaseUrl at construction time
        var baseUrl = settings.Value.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "NpcAI:BaseUrl is not configured. " +
                "Add it to appsettings.json: \"NpcAI\": { \"BaseUrl\": \"http://your-ai-server\" }");

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async IAsyncEnumerable<NpcStreamChunk> SendAudioAndGetResponseAsync(
        byte[] audioBytes,
        NpcWorldDto world,
        string sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();

        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(audioContent, "audio", "audio.wav");
        form.Add(new StringContent(sessionId), "session_id");

        // ✅ FIX: Instead of JSON serializing the world object, send fields directly.
        // This avoids the fragile "Double Serialization" issue.
        form.Add(new StringContent(world.YearRange ?? ""), "year_range");
        form.Add(new StringContent(world.LocationOldName ?? ""), "location_old_name");
        form.Add(new StringContent(world.Civilization ?? ""), "civilization");
        form.Add(new StringContent(world.RoleOrName ?? ""), "role_or_name");

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsync("/npc/stream", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("NPC API error {Status}: {Error}", response.StatusCode, error);
                throw new Exception($"NPC API returned {response.StatusCode}: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call NPC API for session {SessionId}", sessionId);
            throw;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            NpcStreamChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<NpcStreamChunk>(line, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse NPC stream chunk: {Line}", line);
                continue;
            }

            if (chunk != null)
                yield return chunk;
        }
    }
}