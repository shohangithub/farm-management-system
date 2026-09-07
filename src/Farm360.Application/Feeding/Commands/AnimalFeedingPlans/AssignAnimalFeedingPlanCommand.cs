using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public sealed record AssignAnimalFeedingPlanCommand(
    Guid FarmId,
    IReadOnlyList<Guid> FeedingRuleSetIds,
    FeedingPlanType PlanType,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyList<Guid>? AnimalIds = null,
    Guid? BatchId = null,
    Guid? ShedId = null,
    Guid? PenId = null) : IRequest<IReadOnlyList<Guid>>;

public sealed class AssignAnimalFeedingPlanCommandValidator : AbstractValidator<AssignAnimalFeedingPlanCommand>
{
    public AssignAnimalFeedingPlanCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.FeedingRuleSetIds).NotEmpty();
        RuleFor(x => x.PlanType).IsInEnum();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x).Must(x => (x.AnimalIds != null && x.AnimalIds.Count > 0) || x.BatchId.HasValue || x.ShedId.HasValue || x.PenId.HasValue)
            .WithMessage("At least one target (Animal, Batch, Shed, or Pen) must be specified.");
    }
}

public sealed class AssignAnimalFeedingPlanCommandHandler : IRequestHandler<AssignAnimalFeedingPlanCommand, IReadOnlyList<Guid>>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;

    public AssignAnimalFeedingPlanCommandHandler(
        IAnimalFeedingPlanRepository repository,
        IFeedingRuleSetRepository ruleSetRepository,
        IAnimalRepository animalRepository,
        IDailyFeedingEntryRepository entryRepository,
        IUnitOfWork unitOfWork,
        ITenantService tenantService)
    {
        _repository = repository;
        _ruleSetRepository = ruleSetRepository;
        _animalRepository = animalRepository;
        _entryRepository = entryRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<Guid>> Handle(AssignAnimalFeedingPlanCommand request, CancellationToken cancellationToken)
    {
        var createdPlanIds = new List<Guid>();

        var distinctRuleSetIds = request.FeedingRuleSetIds.Distinct().ToList();
        var targetAnimalIds = request.AnimalIds?.Distinct().ToList() ?? new List<Guid>();

        // Duplicate Validation: An animal cannot be enrolled in the same active feeding rule set multiple times
        if (targetAnimalIds.Count > 0)
        {
            var activePlans = await _repository.GetActivePlansByFarmAsync(_tenantService.TenantId, request.FarmId, cancellationToken);
            var duplicateConflicts = new List<ValidationFailure>();

            foreach (var ruleSetId in distinctRuleSetIds)
            {
                var ruleSet = await _ruleSetRepository.GetByIdAsync(ruleSetId, cancellationToken);
                var ruleSetName = ruleSet?.Name ?? "Selected Rule Set";

                foreach (var animalId in targetAnimalIds)
                {
                    if (activePlans.Any(p => p.AnimalId == animalId && p.FeedingRuleSetId == ruleSetId))
                    {
                        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
                        var tag = animal?.Tag?.TagId ?? animalId.ToString();
                        duplicateConflicts.Add(new ValidationFailure("AnimalIds", $"Animal '{tag}' already has an active feeding plan for '{ruleSetName}'."));
                    }
                }
            }

            if (duplicateConflicts.Count > 0)
            {
                throw new Farm360.Application.Common.Exceptions.ValidationException(duplicateConflicts);
            }
        }

        var targets = new List<Guid?>();
        if (targetAnimalIds.Count > 0)
        {
            targets.AddRange(targetAnimalIds.Cast<Guid?>());
        }
        else
        {
            targets.Add(null); // to run at least once for Batch/Shed/Pen
        }

        foreach (var ruleSetId in distinctRuleSetIds)
        {
            var ruleSet = await _ruleSetRepository.GetByIdAsync(ruleSetId, cancellationToken);
            if (ruleSet == null) continue;

            foreach (var animalId in targets)
            {
                var plan = new AnimalFeedingPlan(
                    Guid.NewGuid(),
                    _tenantService.TenantId,
                    request.FarmId,
                    ruleSetId,
                    request.PlanType,
                    request.StartDate,
                    request.EndDate,
                    animalId,
                    request.BatchId,
                    request.ShedId,
                    request.PenId);

                decimal currentWeight = 0;

                if (animalId.HasValue)
                {
                    var animal = await _animalRepository.GetByIdAsync(animalId.Value, cancellationToken);
                    currentWeight = animal?.LatestWeightKg ?? 0;
                }

                var matchingRule = ruleSet.Lines.FirstOrDefault(l =>
                    currentWeight >= l.WeightFromKg && currentWeight < l.WeightToKg);

                matchingRule ??= ruleSet.Lines.OrderBy(l => l.WeightFromKg).FirstOrDefault();

                if (matchingRule != null)
                {
                    decimal expectedKg = matchingRule.ConcentrateKgPerDay;

                    if (ruleSet.PlanType == FeedingPlanType.WeightPercentage)
                    {
                        expectedKg = (currentWeight * matchingRule.ConcentrateKgPerDay) / 100m;
                    }

                    plan.UpdateCurrentRule(
                        matchingRule.Id,
                        currentWeight,
                        expectedKg,
                        matchingRule.RoughageKgPerDay
                    );
                }

                await _repository.AddAsync(plan, cancellationToken);
                createdPlanIds.Add(plan.Id);

                // If plan is active today, immediately generate daily feeding entries for today
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (request.StartDate <= today && (!request.EndDate.HasValue || request.EndDate.Value >= today))
                {
                    var matchingRules = ruleSet.Lines
                        .Where(l => currentWeight >= l.WeightFromKg && currentWeight < l.WeightToKg)
                        .ToList();

                    if (matchingRules.Count == 0 && ruleSet.Lines.Count > 0)
                    {
                        var minWeight = ruleSet.Lines.Min(l => l.WeightFromKg);
                        matchingRules = ruleSet.Lines.Where(l => l.WeightFromKg == minWeight).ToList();
                    }

                    foreach (var ruleLine in matchingRules)
                    {
                        decimal expKg = ruleLine.ConcentrateKgPerDay;
                        if (ruleSet.PlanType == FeedingPlanType.WeightPercentage)
                        {
                            expKg = (currentWeight * ruleLine.ConcentrateKgPerDay) / 100m;
                        }

                        var entry = new DailyFeedingEntry(
                            id: Guid.NewGuid(),
                            tenantId: _tenantService.TenantId,
                            feedingPlanId: plan.Id,
                            farmId: request.FarmId,
                            entryDate: today,
                            formulaId: ruleLine.FormulaId,
                            expectedKg: expKg,
                            ruleLineId: ruleLine.Id,
                            shedId: request.ShedId,
                            penId: request.PenId,
                            batchId: request.BatchId
                        );

                        await _entryRepository.AddAsync(entry, cancellationToken);
                    }
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return createdPlanIds;
    }
}

