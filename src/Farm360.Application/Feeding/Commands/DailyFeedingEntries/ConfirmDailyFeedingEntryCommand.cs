using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

public sealed record ConfirmDailyFeedingEntryCommand(
    Guid EntryId,
    decimal ActualKg,
    Guid? InventoryTransactionId = null,
    string? AdjustmentReason = null) : IRequest;

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

    public ConfirmDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IInventoryItemRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
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

        if (request.ActualKg > 0)
        {
            var formula = await _formulaRepository.GetByIdAsync(entry.FormulaId, cancellationToken);
            if (formula != null)
            {
                var shortfalls = new List<string>();

                foreach (var formulaIngredient in formula.Ingredients)
                {
                    var feedIngredient = await _feedIngredientRepository.GetByIdAsync(formulaIngredient.IngredientId, cancellationToken);
                    if (feedIngredient?.InventoryItemId != null)
                    {
                        var inventoryItem = await _inventoryRepository.GetByIdAsync(feedIngredient.InventoryItemId.Value, cancellationToken);
                        if (inventoryItem != null)
                        {
                            var requiredQty = request.ActualKg * (formulaIngredient.Percentage / 100m);
                            if (requiredQty > 0 && (!inventoryItem.IsActive || inventoryItem.CurrentStock < requiredQty))
                            {
                                shortfalls.Add(
                                    $"Insufficient stock for '{inventoryItem.Name}'. Required: {requiredQty:0.##} {inventoryItem.UnitOfMeasure}, Available: {inventoryItem.CurrentStock:0.##} {inventoryItem.UnitOfMeasure}.");
                            }
                        }
                    }
                }

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

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
    }
}
