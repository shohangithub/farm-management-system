using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.AnimalFeedingPlans;

public sealed record GetFeedingPlansQuery(Guid FarmId, string? Status) : IRequest<IReadOnlyList<AnimalFeedingPlanDto>>;

public sealed record AnimalFeedingPlanDto(
    Guid Id,
    Guid AnimalId,
    Guid RuleSetId,
    string RuleSetName,
    DateOnly AssignedOn,
    DateOnly? CanceledOn,
    bool IsActive,
    decimal ExpectedDailyFeedKg,
    IReadOnlyList<object> Exclusions);

internal sealed class GetFeedingPlansQueryHandler : IRequestHandler<GetFeedingPlansQuery, IReadOnlyList<AnimalFeedingPlanDto>>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly ITenantService _tenantService;

    public GetFeedingPlansQueryHandler(IAnimalFeedingPlanRepository repository, IFeedingRuleSetRepository ruleSetRepository, ITenantService tenantService)
    {
        _repository = repository;
        _ruleSetRepository = ruleSetRepository;
        _tenantService = tenantService;
    }
    
    public async Task<IReadOnlyList<AnimalFeedingPlanDto>> Handle(GetFeedingPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _repository.GetActivePlansByFarmAsync(_tenantService.TenantId, request.FarmId, cancellationToken);
        var ruleSets = await _ruleSetRepository.GetAllAsync(cancellationToken);

        return plans.Select(p => new AnimalFeedingPlanDto(
            p.Id,
            p.AnimalId ?? Guid.Empty,
            p.FeedingRuleSetId,
            ruleSets.FirstOrDefault(r => r.Id == p.FeedingRuleSetId)?.Name ?? "Unknown Rule Set",
            p.StartDate,
            p.Status == FeedingPlanStatus.Cancelled ? p.EndDate : null,
            p.Status == FeedingPlanStatus.Active,
            (p.CurrentConcentrateKgPerDay ?? 0) + (p.CurrentRoughageKgPerDay ?? 0),
            new List<object>()
        )).ToList();
    }
}
