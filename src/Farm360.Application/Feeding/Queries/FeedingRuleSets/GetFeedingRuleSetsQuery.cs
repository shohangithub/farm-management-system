using System.Text.Json.Serialization;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.FeedingRuleSets;

public record FeedingRuleLineDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("minWeightKg")] decimal? MinWeightKg,
    [property: JsonPropertyName("maxWeightKg")] decimal? MaxWeightKg,
    [property: JsonPropertyName("minAgeDays")] int? MinAgeDays,
    [property: JsonPropertyName("maxAgeDays")] int? MaxAgeDays,
    [property: JsonPropertyName("feedType")] FeedCategory FeedType,
    [property: JsonPropertyName("quantityValue")] decimal QuantityValue);

public record FeedingRuleSetDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("planType")] FeedingPlanType PlanType,
    [property: JsonPropertyName("targetAnimalType")] TargetAnimalType TargetAnimalType,
    [property: JsonPropertyName("feedingPurpose")] FeedingPurpose FeedingPurpose,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("baseNotes")] string? BaseNotes,
    [property: JsonPropertyName("rules")] IReadOnlyList<FeedingRuleLineDto> Rules,
    [property: JsonPropertyName("species")] TargetAnimalType Species,
    [property: JsonPropertyName("purpose")] FeedingPurpose Purpose);

public sealed record GetFeedingRuleSetsQuery(
    TargetAnimalType? Species = null,
    FeedingPurpose? Purpose = null) : IRequest<IReadOnlyList<FeedingRuleSetDto>>;

public sealed class GetFeedingRuleSetsQueryHandler : IRequestHandler<GetFeedingRuleSetsQuery, IReadOnlyList<FeedingRuleSetDto>>
{
    private readonly IFeedingRuleSetRepository _repository;
    private readonly ITenantService _tenantService;

    public GetFeedingRuleSetsQueryHandler(IFeedingRuleSetRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<FeedingRuleSetDto>> Handle(GetFeedingRuleSetsQuery request, CancellationToken cancellationToken)
    {
        var ruleSets = await _repository.GetAllAsync(cancellationToken);

        if (request.Species.HasValue)
            ruleSets = ruleSets.Where(x => x.Species == request.Species.Value).ToList();

        if (request.Purpose.HasValue)
            ruleSets = ruleSets.Where(x => x.Purpose == request.Purpose.Value).ToList();

        return ruleSets.Select(r => new FeedingRuleSetDto(
            r.Id,
            r.Name,
            r.PlanType,
            r.Species,
            r.Purpose,
            r.IsActive,
            r.BaseNotes,
            r.Lines.Select(l => new FeedingRuleLineDto(
                l.Id,
                l.MinWeightKg,
                l.MaxWeightKg,
                l.MinAgeDays,
                l.MaxAgeDays,
                l.FeedType,
                l.QuantityValue
            )).ToList(),
            r.Species,
            r.Purpose)).ToList();
    }
}
