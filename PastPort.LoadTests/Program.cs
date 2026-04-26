using System.Diagnostics;
using NBomber.Configuration;
using NBomber.CSharp;
using PastPort.LoadTests.Clients;
using PastPort.LoadTests.Scenarios;

namespace PastPort.LoadTests;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting PastPort NBomber Load Tests");
        var baseUrl = "http://localhost:5263"; // Default for local tests
        var userCount = 300; // Matches max connections
        var tokens = new List<string>();

        Console.WriteLine($"Initializing {userCount} virtual users. This may take a minute...");
        
        var initTasks = new List<Task>();
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

        for (int i = 0; i < userCount; i++)
        {
            var email = $"loadtestuser{i}_{Guid.NewGuid().ToString("N")[..8]}@example.com";
            var password = "Password123!";
            
            initTasks.Add(Task.Run(async () =>
            {
                var client = new PastPortApiClient(baseUrl);
                var token = await client.RegisterAndLoginAsync(email, password);
                if (token != null)
                {
                    lock (tokens)
                    {
                        tokens.Add(token);
                    }
                }
            }));

            // Throttle slightly to not kill local dev env with 300 simultaneous registrations
            if (i % 20 == 0)
                await Task.Delay(100); 
        }

        await Task.WhenAll(initTasks);
        
        Console.WriteLine($"Successfully initialized {tokens.Count} users with JWT tokens.");
        if (tokens.Count == 0)
        {
            Console.WriteLine("Failed to initialize any users. Is the backend running at " + baseUrl + "?");
            return;
        }

        var scenario = NpcStreamingScenario.Create(baseUrl, tokens);

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .Run();
    }
}
