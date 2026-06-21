using PastPort.Application.DTOs.Response;

namespace PastPort.Application.Interfaces;

public interface IVrEnvironmentService
{
    Task<bool> CheckHealthAsync();

    Task<SceneGenerationResponseDto> GenerateSceneAsync(
        string civilization,
        string yearRange,
        string locationOldName,
        string? roleOrName);

    Task<Stream> GenerateAssetAsync(
        string prompt,
        bool isNpc,
        CancellationToken cancellationToken);
}