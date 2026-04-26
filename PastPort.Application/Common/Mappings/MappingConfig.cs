using Mapster;
using PastPort.Application.DTOs;
using PastPort.Application.DTOs.Response;
using PastPort.Domain.Entities;

namespace PastPort.Application.Common.Mappings;

public sealed class MappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // HistoricalScene -> SceneResponseDto
        config.NewConfig<HistoricalScene, SceneResponseDto>()
            .Map(dest => dest.CharactersCount, src => src.Characters != null ? src.Characters.Count : 0)
            .Map(dest => dest.Characters, src => src.Characters);

        // UserSubscription -> UserSubscriptionDto
        config.NewConfig<UserSubscription, UserSubscriptionDto>()
            .Map(dest => dest.Plan, src => src.Plan);

        // Plan -> PlanDto
        config.NewConfig<Plan, PlanDto>()
            .Map(dest => dest.Features, src => src.PlanFeatures.Where(pf => pf.Feature.IsActive));
            
        // PlanFeature -> FeatureDto
        config.NewConfig<PlanFeature, FeatureDto>()
            .Map(dest => dest.Id, src => src.Feature.Id)
            .Map(dest => dest.Name, src => src.Feature.Name)
            .Map(dest => dest.Slug, src => src.Feature.Slug)
            .Map(dest => dest.Description, src => src.Feature.Description)
            .Map(dest => dest.Limit, src => src.Limit)
            .Map(dest => dest.IsEnabled, src => src.IsEnabled);
    }
}
