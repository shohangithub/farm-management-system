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
