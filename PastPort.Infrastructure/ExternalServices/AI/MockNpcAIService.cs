using System.Runtime.CompilerServices;
using PastPort.Application.Interfaces;
using PastPort.Application.Models.Npc;

namespace PastPort.Infrastructure.ExternalServices.AI;

public sealed class MockNpcAIService : INpcAIService
{
    public async IAsyncEnumerable<NpcStreamChunk> StreamConversationAsync(
        byte[] audioBytes,
        NpcSessionData sessionData,
        string roleOrName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Send Meta Chunk
        yield return new MetaChunk(
            Text: $"Hello! I am a mock response for {roleOrName} in {sessionData.Civilization}.",
            Emotion: "Happy",
            CurrentYear: 2024
        );

        await Task.Delay(100, cancellationToken);

        // 2. Send some dummy audio chunks
        for (int i = 0; i < 3; i++)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            
            yield return new AudioChunk(new byte[1024]); // 1KB dummy audio
            await Task.Delay(50, cancellationToken);
        }

        // 3. Send Done Chunk
        yield return new DoneChunk();
    }
}
