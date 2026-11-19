using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Exceptions;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

/// <summary>
/// Confirms a day's feeding entry. Transactional: the entry, the stock deduction, the cost snapshot,
/// the per-animal allocations and the ledger postings (all written by the
/// <see cref="Farm360.Domain.Feeding.Events.DailyEntryConfirmedEvent"/> handlers, which run inside
/// this command's transaction) commit together or not at all.
/// </summary>
public sealed record ConfirmDailyFeedingEntryCommand(
    Guid EntryId,
    decimal ActualKg,
    Guid? InventoryTransactionId = null,
    string? AdjustmentReason = null) : IRequest, ITransactionalCommand;

public sealed class ConfirmDailyFeedingEntryCommandValidator : AbstractValidator<ConfirmDailyFeedingEntryCommand>
{
    public ConfirmDailyFeedingEntryCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.ActualKg).GreaterThanOrEqualTo(0);
    }
}

public sealed class ConfirmDailyFeedingEntryCommandHandler : IRequestHandler<ConfirmDailyFeedingEntryCommand>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IAnimalFeedingPlanRepository? _planRepository;
    private readonly IAnimalFeedAllocationRepository? _allocationRepository;
    private readonly IFarmBusinessRulesProvider? _rules;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;
    private readonly Farm360.Domain.Livestock.Repositories.IAnimalRepository? _animals;

    public ConfirmDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IInventoryItemRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
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
    }

    public async Task Handle(ConfirmDailyFeedingEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(DailyFeedingEntry), request.EntryId);

        // Double submit (two taps, two devices): the first confirmation already deducted stock and
        // booked the cost. A second one must be refused clearly, never deduct again.
        if (entry.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted)
            throw new ConflictException("This feeding entry is already confirmed; its stock and cost have already been posted.");

        // Farm rule: an animal is fed only while on the farm (acquired, not yet sold or dead on the
        // entry's date). An individual entry for a day it was gone is refused, with the reason.
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

        entry.Confirm(request.ActualKg, request.InventoryTransactionId, request.AdjustmentReason);

        var domainEvents = entry.DomainEvents.ToList();
        entry.ClearDomainEvents();

        _repository.Update(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Handlers run inside this command's transaction (TransactionBehavior); one that fails
        // throws, and the entry's confirmation above is rolled back with everything else.
        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
    }
}
