using Farm360.Application.Feeding.Services;
using System.Text.Json.Serialization;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.FeedingRuleSets;

public record UpdateFeedingRuleLineDto(
    [property: JsonPropertyName("id")] Guid? Id,
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
    [property: JsonPropertyName("proteinTargetPercent")] decimal? ProteinTargetPercent = null,
    [property: JsonPropertyName("inventoryItemId")] Guid? InventoryItemId = null);

public record UpdateFeedingRuleSetCommand(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("planType")] FeedingPlanType PlanType,
    [property: JsonPropertyName("targetAnimalType")] TargetAnimalType Species,
    [property: JsonPropertyName("feedingPurpose")] FeedingPurpose Purpose,
    [property: JsonPropertyName("isActive")] bool IsActive = true,
    [property: JsonPropertyName("baseNotes")] string? BaseNotes = null,
    [property: JsonPropertyName("breedId")] Guid? BreedId = null,
    [property: JsonPropertyName("ageFromDays")] int? AgeFromDays = null,
    [property: JsonPropertyName("ageToDays")] int? AgeToDays = null,
    [property: JsonPropertyName("rules")] IReadOnlyCollection<UpdateFeedingRuleLineDto>? Rules = null,
    [property: JsonPropertyName("lines")] IReadOnlyCollection<UpdateFeedingRuleLineDto>? Lines = null) : IRequest
{
    public IReadOnlyCollection<UpdateFeedingRuleLineDto> RuleLines => Rules ?? Lines ?? Array.Empty<UpdateFeedingRuleLineDto>();
}

public sealed class UpdateFeedingRuleSetCommandValidator : AbstractValidator<UpdateFeedingRuleSetCommand>
{
    public UpdateFeedingRuleSetCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Species).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
        RuleFor(x => x.RuleLines).NotEmpty().WithMessage("'Lines' must not be empty.");
        RuleForEach(x => x.RuleLines).ChildRules(line =>
        {
            line.RuleFor(l => l.QuantityValue).GreaterThanOrEqualTo(0);
        });

        // See CreateFeedingRuleSetCommandValidator: the same quantity field means a kg/day amount
        // or a body-weight percentage depending on PlanType, with nothing to distinguish which.
        // Every line is resubmitted on update (ClearLines + re-add below), so this also catches a
        // PlanType changed here without the line values being rescaled to match.
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

public class UpdateFeedingRuleSetCommandHandler : IRequestHandler<UpdateFeedingRuleSetCommand>
{
    private readonly IFeedingRuleSetRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateFeedingRuleSetCommandHandler> _logger;
    private readonly IDirectFeedService _directFeed;

    public UpdateFeedingRuleSetCommandHandler(
        IFeedingRuleSetRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateFeedingRuleSetCommandHandler> logger,
        IDirectFeedService directFeed)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _directFeed = directFeed;
    }

    public async Task Handle(UpdateFeedingRuleSetCommand request, CancellationToken cancellationToken)
    {
        var ruleSet = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.FeedingRuleSet), request.Id);

        ruleSet.UpdateDetails(
            request.Name,
            request.Species,
            request.Purpose,
            request.PlanType,
            request.BaseNotes,
            request.BreedId,
            request.AgeFromDays,
            request.AgeToDays);
            
        ruleSet.SetActiveStatus(request.IsActive);
        
        ruleSet.ClearLines();
        
        foreach (var rule in request.RuleLines)
        {
            var formulaId = rule.InventoryItemId is { } itemId
                ? (await _directFeed.GetOrCreateItemFormulaAsync(itemId, request.Species, cancellationToken)).Id
                : rule.FormulaId;

            ruleSet.AddRuleLine(
                rule.MinWeightKg, 
                rule.MaxWeightKg,
                rule.MinAgeDays,
                rule.MaxAgeDays,
                rule.FeedType,
                rule.QuantityValue > 0 ? rule.QuantityValue : rule.ConcentrateKgPerDay,
                formulaId,
                rule.SessionsPerDay > 0 ? rule.SessionsPerDay : 1);
        }

        _repository.Update(ruleSet);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Updated feeding rule set {RuleSetId}", ruleSet.Id);
    }
}
