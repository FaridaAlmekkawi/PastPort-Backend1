// PastPort.Application/Models/Npc/NpcSessionData.cs
namespace PastPort.Application.Models.Npc;

/// <summary>
/// Stored in IMemoryCache keyed by sessionId.
/// Intentionally a sealed record — immutable after creation.
/// </summary>
public sealed record NpcSessionData(
    string YearRange,
    string LocationOldName,
    string Civilization,
    DateTime CreatedAt
);

/// <summary>
/// A single streamed chunk from the LLM WebSocket.
/// Discriminated by ChunkType — hub pattern-matches on this.
/// </summary>
public abstract record NpcStreamChunk;

public sealed record MetaChunk(
    string Text,
    string Emotion,
    int CurrentYear
) : NpcStreamChunk;

public sealed record AudioChunk(
    byte[] Bytes
) : NpcStreamChunk;

public sealed record DoneChunk() : NpcStreamChunk;

public sealed record ErrorChunk(string Reason) : NpcStreamChunk;