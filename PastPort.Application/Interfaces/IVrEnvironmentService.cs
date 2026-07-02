using PastPort.Application.DTOs.Response;

namespace PastPort.Application.Interfaces;

public interface IVrEnvironmentService
{
    Task<bool> CheckHealthAsync();

    Task<SceneGenerationResponseDto> GenerateSceneAsync(
        string civilization,
        string locationOldName,
        string goal,
        string? roleOrName);

    Task<ManualSceneLayoutResponseDto> GenerateManualLayoutAsync(
        ManualSceneLayoutRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Stream> GenerateAssetAsync(
        string prompt,
        bool isNpc,
        CancellationToken cancellationToken);
}
