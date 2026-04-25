// PastPort.Application/Interfaces/INpcAIService.cs
using PastPort.Application.Models.Npc;

namespace PastPort.Application.Interfaces;

/// <summary>
/// Opens a ClientWebSocket to the Python LLM, sends the audio+world payload,
/// and streams typed chunks back via IAsyncEnumerable.
///
/// The hub iterates the enumerable and pushes each chunk to Unity —
/// this keeps the hub clean and the service fully testable.
/// </summary>
public interface INpcAIService
{
    /// <summary>
    /// Sends audio bytes and world context to the LLM over WebSocket,
    /// then streams typed response chunks back to the caller.
    ///
    /// Completes when the LLM sends {"type":"done"} or the token is cancelled.
    /// Yields an ErrorChunk (never throws) so the hub can relay the failure
    /// to Unity without crashing the hub pipeline.
    /// </summary>
    IAsyncEnumerable<NpcStreamChunk> StreamConversationAsync(
        byte[] audioBytes,
        NpcSessionData sessionData,
        string roleOrName,
        CancellationToken cancellationToken = default
    );
}