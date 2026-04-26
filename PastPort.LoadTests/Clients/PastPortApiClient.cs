using System.Net.Http.Json;
using System.Text.Json;

namespace PastPort.LoadTests.Clients;

public class PastPortApiClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public PastPortApiClient(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public void SetToken(string token)
    {
        _token = token;
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string?> RegisterAndLoginAsync(string email, string password)
    {
        // 1. Register
        var registerResponse = await _httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            confirmPassword = password,
            firstName = "LoadTest",
            lastName = "User"
        });

        if (!registerResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"Register failed for {email}: {await registerResponse.Content.ReadAsStringAsync()}");
            return null;
        }

        var data = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var success = data.GetProperty("success").GetBoolean();
        if (!success)
        {
            Console.WriteLine($"Register returned failure for {email}: {data.GetProperty("message").GetString()}");
            return null;
        }

        var token = data.GetProperty("token").GetString();
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine($"Token was null for {email}");
            return null;
        }

        SetToken(token);
        return token;
    }

    public async Task<string?> StartNpcSessionAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("/api/npc/session/start", new
        {
            yearRange = "1000-1100",
            locationOldName = "Cairo",
            civilization = "Fatimid"
        });

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to start session: {err}");
        }

        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        return data.GetProperty("sessionId").GetString();
    }
}
