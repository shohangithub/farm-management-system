using System.Text.Json.Serialization;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedingRuleSets;

public record FeedingRuleLineRequest(
    [property: JsonPropertyName("minWeightKg")] decimal? MinWeightKg,
    [property: JsonPropertyName("maxWeightKg")] decimal? MaxWeightKg,
    [property: JsonPropertyName("minAgeDays")] int? MinAgeDays,
    [property: JsonPropertyName("maxAgeDays")] int? MaxAgeDays,
    [property: JsonPropertyName("feedType")] FeedCategory FeedType,
    [property: JsonPropertyName("quantityValue")] decimal QuantityValue,
    [property: JsonPropertyName("weightFromKg")] decimal WeightFromKg = 0,
    [property: JsonPropertyName("weightToKg")] decimal WeightToKg = 0,
    [property: JsonPropertyName("formulaId")] Guid? FormulaId = null,
    [property: JsonPropertyName("concentrateKgPerDay")] decimal ConcentrateKgPerDay = 0,
    [property: JsonPropertyName("roughageKgPerDay")] decimal RoughageKgPerDay = 0,
    [property: JsonPropertyName("sessionsPerDay")] int SessionsPerDay = 1,
    [property: JsonPropertyName("proteinTargetPercent")] decimal? ProteinTargetPercent = null);

public sealed record CreateFeedingRuleSetCommand(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("planType")] FeedingPlanType PlanType,
    [property: JsonPropertyName("targetAnimalType")] TargetAnimalType Species,
    [property: JsonPropertyName("feedingPurpose")] FeedingPurpose Purpose,
    [property: JsonPropertyName("isActive")] bool IsActive = true,
    [property: JsonPropertyName("baseNotes")] string? BaseNotes = null,
    [property: JsonPropertyName("breedId")] Guid? BreedId = null,
    [property: JsonPropertyName("ageFromDays")] int? AgeFromDays = null,
    [property: JsonPropertyName("ageToDays")] int? AgeToDays = null,
    [property: JsonPropertyName("rules")] IReadOnlyList<FeedingRuleLineRequest>? Rules = null,
    [property: JsonPropertyName("lines")] IReadOnlyList<FeedingRuleLineRequest>? Lines = null) : IRequest<Guid>
{
    public IReadOnlyList<FeedingRuleLineRequest> RuleLines => Rules ?? Lines ?? Array.Empty<FeedingRuleLineRequest>();
}

public sealed class CreateFeedingRuleSetCommandValidator : AbstractValidator<CreateFeedingRuleSetCommand>
{
    public CreateFeedingRuleSetCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Species).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
        RuleFor(x => x.RuleLines).NotEmpty().WithMessage("'Lines' must not be empty.");
        RuleForEach(x => x.RuleLines).ChildRules(line =>
        {
            line.RuleFor(l => l.QuantityValue).GreaterThanOrEqualTo(0);
        });

        // ConcentrateKgPerDay/QuantityValue means two different things depending on PlanType -- an
        // absolute kg/day amount in FixedQuantity mode, a PERCENT of body weight in WeightPercentage
        // mode -- with nothing distinguishing which was intended. A rule authored under the wrong
        // mode (or a PlanType changed after the fact, which doesn't rescale existing values) used to
        // silently produce a ration roughly 100x off. This at least catches the case a percentage
        // field holds something that obviously isn't a percentage.
        RuleForEach(x => x.RuleLines)
            .Must((command, line) =>
            {
                if (command.PlanType != FeedingPlanType.WeightPercentage)
                {
                    return true;
                }

                var effectiveValue = line.QuantityValue > 0 ? line.QuantityValue : line.ConcentrateKgPerDay;
                return effectiveValue > 0 && effectiveValue <= 100;
            })
            .WithMessage("With Plan Type 'Weight Percentage', each line's quantity is a PERCENT of body weight (e.g. 3 for 3%), not an absolute kg/day amount -- it must be between 0 and 100.");
    }
}

public sealed class CreateFeedingRuleSetCommandHandler : IRequestHandler<CreateFeedingRuleSetCommand, Guid>
{
    private readonly IFeedingRuleSetRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;

    public CreateFeedingRuleSetCommandHandler(
        IFeedingRuleSetRepository repository,
        IUnitOfWork unitOfWork,
        ITenantService tenantService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Guid> Handle(CreateFeedingRuleSetCommand request, CancellationToken cancellationToken)
    {
        var ruleSet = new FeedingRuleSet(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.Name,
            request.Species,
            request.Purpose,
            request.PlanType,
            request.BaseNotes,
            request.BreedId,
            request.AgeFromDays,
            request.AgeToDays,
            request.IsActive);

        foreach (var line in request.RuleLines)
        {
            ruleSet.AddRuleLine(
                line.MinWeightKg,
                line.MaxWeightKg,
                line.MinAgeDays,
                line.MaxAgeDays,
                line.FeedType,
                line.QuantityValue > 0 ? line.QuantityValue : line.ConcentrateKgPerDay,
                line.FormulaId,
                line.SessionsPerDay > 0 ? line.SessionsPerDay : 1);
        }

        await _repository.AddAsync(ruleSet, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ruleSet.Id;
    }
}
