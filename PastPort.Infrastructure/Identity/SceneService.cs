using Mapster;
using MapsterMapper;
using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;

namespace PastPort.Infrastructure.Identity;

public class SceneService : ISceneService
{
    private readonly ISceneRepository _sceneRepository;
    private readonly IMapper _mapper;

    public SceneService(ISceneRepository sceneRepository, IMapper mapper)
    {
        _sceneRepository = sceneRepository;
        _mapper = mapper;
    }

    public async Task<SceneResponseDto> CreateSceneAsync(CreateSceneRequestDto request)
    {
        var scene = request.Adapt<HistoricalScene>();
        scene.Id = Guid.NewGuid();
        scene.CreatedAt = DateTime.UtcNow;

        await _sceneRepository.AddAsync(scene);
        return _mapper.Map<SceneResponseDto>(scene);
    }

    public async Task<SceneResponseDto> GetSceneByIdAsync(Guid id)
    {
        var scene = await _sceneRepository.GetSceneWithCharactersAsync(id)
            ?? throw new Exception("Scene not found");
        return _mapper.Map<SceneResponseDto>(scene);
    }

    public async Task<List<SceneResponseDto>> GetAllScenesAsync()
    {
        var scenes = await _sceneRepository.GetAllAsync();
        return _mapper.Map<List<SceneResponseDto>>(scenes);
    }

    public async Task<List<SceneResponseDto>> GetScenesByEraAsync(string era)
    {
        var scenes = await _sceneRepository.GetByEraAsync(era);
        return _mapper.Map<List<SceneResponseDto>>(scenes);
    }

    public async Task<List<SceneResponseDto>> SearchScenesAsync(string searchTerm)
    {
        var scenes = await _sceneRepository.SearchScenesAsync(searchTerm);
        return _mapper.Map<List<SceneResponseDto>>(scenes);
    }

    public async Task<SceneResponseDto> UpdateSceneAsync(Guid id, UpdateSceneRequestDto request)
    {
        var scene = await _sceneRepository.GetByIdAsync(id)
            ?? throw new Exception("Scene not found");

        request.Adapt(scene);
        scene.UpdatedAt = DateTime.UtcNow;

        await _sceneRepository.UpdateAsync(scene);
        return _mapper.Map<SceneResponseDto>(scene);
    }

    public async Task<bool> DeleteSceneAsync(Guid id)
    {
        var scene = await _sceneRepository.GetByIdAsync(id)
            ?? throw new Exception("Scene not found");
        await _sceneRepository.DeleteAsync(scene);
        return true;
    }
}