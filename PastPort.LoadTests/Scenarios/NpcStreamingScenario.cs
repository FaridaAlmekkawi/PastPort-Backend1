using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts;
using NBomber.CSharp;
using PastPort.LoadTests.Clients;

namespace PastPort.LoadTests.Scenarios;

public static class NpcStreamingScenario
{
    // Generate ~500KB of dummy data representing some audio chunks
    private static readonly byte[][] DummyAudioChunks = Enumerable.Range(0, 10)
        .Select(_ => new byte[50 * 1024]) // 10 chunks of 50KB each
        .ToArray();

    public static ScenarioProps Create(string baseUrl, List<string> tokens)
    {
        return Scenario.Create("npc_streaming_scenario", async context =>
        {
            var step = await Step.Run("npc_streaming_step", context, async () =>
            {
                // 1. Pick a random token from the pool
                var random = new Random();
                var token = tokens[random.Next(tokens.Count)];

                var apiClient = new PastPortApiClient(baseUrl);
                apiClient.SetToken(token);

                // 2. Start Session
                string sessionId;
                try
                {
                    sessionId = await apiClient.StartNpcSessionAsync() ?? throw new Exception("Session ID was null");
                }
                catch (Exception ex)
                {
                    context.Logger.Error(ex, "Failed to start NPC session");
                    return Response.Fail();
                }

                // 3. Connect to SignalR
                var hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{baseUrl}/npcHub", options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    })
                    .Build();

                var completionTcs = new TaskCompletionSource<bool>();
                int chunksReceived = 0;

                hubConnection.On<object>("OnMetaReceived", meta => { });
                hubConnection.On<byte[]>("OnAudioReceived", bytes => { chunksReceived++; });
                hubConnection.On("OnConversationDone", () => { completionTcs.TrySetResult(true); });
                hubConnection.On<string>("OnSessionError", err => { completionTcs.TrySetException(new Exception($"Session Error: {err}")); });

                try
                {
                    await hubConnection.StartAsync();

                    // 4. Create an async enumerable stream of dummy audio chunks
                    var channel = Channel.CreateUnbounded<byte[]>();
                    var sendTask = Task.Run(async () =>
                    {
                        foreach (var chunk in DummyAudioChunks)
                        {
                            await channel.Writer.WriteAsync(chunk);
                            await Task.Delay(50); // Simulate streaming real-time delay
                        }
                        channel.Writer.Complete();
                    });

                    // 5. Invoke Hub
                    await hubConnection.InvokeAsync("StartConversation", sessionId, "TestRole", channel.Reader.ReadAllAsync());

                    // 6. Wait for Done
                    var completedTask = await Task.WhenAny(completionTcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
                    if (completedTask != completionTcs.Task)
                    {
                        return Response.Fail();
                    }

                    return Response.Ok(sizeBytes: chunksReceived * 1024); // Dummy size recording
                }
                catch (Exception ex)
                {
                    context.Logger.Error(ex, "SignalR error");
                    return Response.Fail();
                }
                finally
                {
                    await hubConnection.StopAsync();
                    await hubConnection.DisposeAsync();
                }
            });

            return step;
        })
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 50, during: TimeSpan.FromSeconds(30)),   // Baseline
            Simulation.KeepConstant(copies: 100, during: TimeSpan.FromSeconds(30)),  // Stable target
            Simulation.KeepConstant(copies: 200, during: TimeSpan.FromSeconds(30)),  // Stress ramp
            Simulation.KeepConstant(copies: 300, during: TimeSpan.FromSeconds(30))   // Stress peak
        );
    }
}
