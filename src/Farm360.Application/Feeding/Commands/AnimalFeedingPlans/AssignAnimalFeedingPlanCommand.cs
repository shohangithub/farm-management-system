using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public sealed record AssignAnimalFeedingPlanCommand(
    Guid FarmId,
    Guid FeedingRuleSetId,
    FeedingPlanType PlanType,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? AnimalId = null,
    Guid? BatchId = null,
    Guid? ShedId = null,
    Guid? PenId = null) : IRequest<Guid>;

public sealed class AssignAnimalFeedingPlanCommandValidator : AbstractValidator<AssignAnimalFeedingPlanCommand>
{
    public AssignAnimalFeedingPlanCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.FeedingRuleSetId).NotEmpty();
        RuleFor(x => x.PlanType).IsInEnum();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x).Must(x => x.AnimalId.HasValue || x.BatchId.HasValue || x.ShedId.HasValue || x.PenId.HasValue)
            .WithMessage("At least one target (Animal, Batch, Shed, or Pen) must be specified.");
    }
}

public sealed class AssignAnimalFeedingPlanCommandHandler : IRequestHandler<AssignAnimalFeedingPlanCommand, Guid>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;

    public AssignAnimalFeedingPlanCommandHandler(
        IAnimalFeedingPlanRepository repository,
        IFeedingRuleSetRepository ruleSetRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork,
        ITenantService tenantService)
    {
        _repository = repository;
        _ruleSetRepository = ruleSetRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Guid> Handle(AssignAnimalFeedingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = new AnimalFeedingPlan(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.FarmId,
            request.FeedingRuleSetId,
            request.PlanType,
            request.StartDate,
            request.EndDate,
            request.AnimalId,
            request.BatchId,
            request.ShedId,
            request.PenId);

        var ruleSet = await _ruleSetRepository.GetByIdAsync(request.FeedingRuleSetId, cancellationToken);
        if (ruleSet != null)
        {
            decimal currentWeight = 0;

            if (request.AnimalId.HasValue)
            {
                var animal = await _animalRepository.GetByIdAsync(request.AnimalId.Value, cancellationToken);
                currentWeight = animal?.LatestWeightKg ?? 0;
            }

            var matchingRule = ruleSet.Lines.FirstOrDefault(l => 
                currentWeight >= l.WeightFromKg && currentWeight < l.WeightToKg);

            matchingRule ??= ruleSet.Lines.OrderBy(l => l.WeightFromKg).FirstOrDefault();

            if (matchingRule != null)
            {
                plan.UpdateCurrentRule(
                    matchingRule.Id,
                    currentWeight,
                    matchingRule.ConcentrateKgPerDay,
                    matchingRule.RoughageKgPerDay
                );
            }
        }

        await _repository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return plan.Id;
    }
}
