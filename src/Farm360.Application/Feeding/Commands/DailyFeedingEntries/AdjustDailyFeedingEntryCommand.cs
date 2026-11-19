using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Exceptions;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

/// <summary>Confirms an entry with a different quantity. Transactional, like <see cref="ConfirmDailyFeedingEntryCommand"/>.</summary>
public record AdjustDailyFeedingEntryCommand(
    Guid EntryId,
    decimal ActualKg,
    string? Notes) : IRequest, ITransactionalCommand;

public class AdjustDailyFeedingEntryCommandHandler : IRequestHandler<AdjustDailyFeedingEntryCommand>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<AdjustDailyFeedingEntryCommandHandler> _logger;
    private readonly IAnimalFeedingPlanRepository? _planRepository;
    private readonly IAnimalFeedAllocationRepository? _allocationRepository;
    private readonly IFarmBusinessRulesProvider? _rules;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;
    private readonly Farm360.Domain.Livestock.Repositories.IAnimalRepository? _animals;

    public AdjustDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IInventoryItemRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<AdjustDailyFeedingEntryCommandHandler> logger,
        IAnimalFeedingPlanRepository? planRepository = null,
        IAnimalFeedAllocationRepository? allocationRepository = null,
        IFarmBusinessRulesProvider? rules = null,
        Farm360.Application.Inventory.Units.IUnitService? units = null,
        Farm360.Domain.Livestock.Repositories.IAnimalRepository? animals = null)
    {
        _animals = animals;
        _planRepository = planRepository;
        _allocationRepository = allocationRepository;
        _rules = rules;
        _units = units;
        _repository = repository;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(AdjustDailyFeedingEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(DailyFeedingEntry), request.EntryId);

        // Confirm() silently ignores an entry that is already confirmed, so an adjustment would have
        // returned success while changing nothing (stock and cost already posted). Say so instead.
        if (entry.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted)
            throw new ConflictException("This feeding entry is already confirmed; its stock and cost have been posted and it can no longer be adjusted.");

        // Farm rule: an animal is fed only while on the farm (acquired, not yet sold or dead on the
        // entry's date). An individual entry for a day it was gone is refused, with the reason.
        if (entry.Status is not (DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted))
            await FeedingPresenceGuard.EnsureAnimalOnFarmAsync(entry, _planRepository, _animals, cancellationToken);

        // The same requirement the confirmation handler deducts: quantity x heads fed (honouring the
        // group-plan quantity basis), split by normalised recipe %, converted to each item's base unit.
        if (request.ActualKg > 0)
        {
            var formula = await _formulaRepository.GetByIdAsync(entry.FormulaId, cancellationToken);
            if (formula != null)
            {
                var basis = _rules?.Current.GroupPlanQuantityBasis ?? Farm360.Domain.BusinessRules.GroupPlanQuantityBasis.PerHead;
                var heads = await FeedEntryHeads.MultiplierAsync(entry, _planRepository, _allocationRepository, basis, cancellationToken);
                var calculator = new FeedCostCalculator(_feedIngredientRepository, _inventoryRepository, _units);
                var requirement = await calculator.RequirementAsync(formula, request.ActualKg * heads, cancellationToken);
                var shortfalls = requirement.Shortfalls();
                if (shortfalls.Count > 0)
                {
                    throw new DomainException(string.Join(" ", shortfalls));
                }
            }
        }

        entry.Confirm(request.ActualKg, null, request.Notes);
        
        var domainEvents = entry.DomainEvents.ToList();
        entry.ClearDomainEvents();

        _repository.Update(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Adjusted feeding entry {EntryId} with actual {ActualKg} kg", entry.Id, request.ActualKg);
    }
}
