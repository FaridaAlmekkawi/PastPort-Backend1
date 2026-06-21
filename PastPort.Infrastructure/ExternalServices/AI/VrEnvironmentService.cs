using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PastPort.Application.Common;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;

namespace PastPort.Infrastructure.ExternalServices.AI;

public class VrEnvironmentService : IVrEnvironmentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VrEnvironmentService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public VrEnvironmentService(
        HttpClient httpClient,
        IOptions<VrGeneratorSettings> settings,
        ILogger<VrEnvironmentService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VR Generator health check failed");
            return false;
        }
    }

    public async Task<SceneGenerationResponseDto> GenerateSceneAsync(
        string civilization,
        string yearRange,
        string locationOldName,
        string? roleOrName)
    {
        var query = $"/scene?civilization={Uri.EscapeDataString(civilization)}" +
                    $"&year_range={Uri.EscapeDataString(yearRange)}" +
                    $"&location_old_name={Uri.EscapeDataString(locationOldName)}";

        if (!string.IsNullOrWhiteSpace(roleOrName))
            query += $"&role_or_name={Uri.EscapeDataString(roleOrName)}";

        _logger.LogInformation("Requesting scene generation: {Civ} ({Range})", civilization, yearRange);

        // ⚠️ الطلب ده بياخد لحد 7 دقايق، الـ HttpClient timeout اتظبط في Program.cs
        var response = await _httpClient.GetAsync(query);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Scene generation failed {Status}: {Error}", response.StatusCode, error);
            throw new Exception($"Scene generation failed: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<SceneGenerationResponseDto>(_jsonOptions);

        if (result == null)
            throw new Exception("Scene generation returned empty response");

        return result;
    }

    public async Task<Stream> GenerateAssetAsync(
        string prompt,
        bool isNpc,
        CancellationToken cancellationToken)
    {
        var query = $"/asset?prompt={Uri.EscapeDataString(prompt)}&is_npc={isNpc.ToString().ToLowerInvariant()}";

        _logger.LogInformation("Requesting asset generation, isNpc={IsNpc}", isNpc);

        var response = await _httpClient.GetAsync(query, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Asset generation failed {Status}: {Error}", response.StatusCode, error);
            throw new Exception($"Asset generation failed: {response.StatusCode}");
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}