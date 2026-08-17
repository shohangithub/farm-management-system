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
    [property: JsonPropertyName("proteinTargetPercent")] decimal? ProteinTargetPercent = null);

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
    }
}

public class UpdateFeedingRuleSetCommandHandler : IRequestHandler<UpdateFeedingRuleSetCommand>
{
    private readonly IFeedingRuleSetRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateFeedingRuleSetCommandHandler> _logger;

    public UpdateFeedingRuleSetCommandHandler(
        IFeedingRuleSetRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateFeedingRuleSetCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
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
            ruleSet.AddRuleLine(
                rule.MinWeightKg, 
                rule.MaxWeightKg,
                rule.MinAgeDays,
                rule.MaxAgeDays,
                rule.FeedType,
                rule.QuantityValue > 0 ? rule.QuantityValue : rule.ConcentrateKgPerDay,
                rule.FormulaId,
                rule.SessionsPerDay > 0 ? rule.SessionsPerDay : 1);
        }

        _repository.Update(ruleSet);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Updated feeding rule set {RuleSetId}", ruleSet.Id);
    }
}
