using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.Commands.Consumables;

public sealed record ConfirmDailyConsumableEntryCommand(
    Guid EntryId,
    decimal ActualQuantity,
    string? AdjustmentReason = null) : IRequest;

public sealed class ConfirmDailyConsumableEntryCommandValidator : AbstractValidator<ConfirmDailyConsumableEntryCommand>
{
    public ConfirmDailyConsumableEntryCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.ActualQuantity).GreaterThanOrEqualTo(0);
    }
}

public sealed class ConfirmDailyConsumableEntryCommandHandler : IRequestHandler<ConfirmDailyConsumableEntryCommand>
{
    private readonly IDailyConsumableEntryRepository _entryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _stockTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<ConfirmDailyConsumableEntryCommandHandler> _logger;

    public ConfirmDailyConsumableEntryCommandHandler(
        IDailyConsumableEntryRepository entryRepository,
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository stockTransactionRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<ConfirmDailyConsumableEntryCommandHandler> logger)
    {
        _entryRepository = entryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _stockTransactionRepository = stockTransactionRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(ConfirmDailyConsumableEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(DailyConsumableEntry), request.EntryId);

        if (entry.Status == DailyConsumableEntryStatus.Confirmed || entry.Status == DailyConsumableEntryStatus.Adjusted)
        {
            return; // Already confirmed
        }

        var inventoryItem = await _inventoryItemRepository.GetByIdAsync(entry.InventoryItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), entry.InventoryItemId);

        if (request.ActualQuantity > 0)
        {
            if (!inventoryItem.IsActive)
            {
                throw new DomainException($"Inventory item '{inventoryItem.Name}' is inactive.");
            }

            if (inventoryItem.CurrentStock < request.ActualQuantity)
            {
                throw new DomainException(
                    $"Insufficient stock for '{inventoryItem.Name}'. Required: {request.ActualQuantity:0.##} {inventoryItem.UnitOfMeasure}, Available: {inventoryItem.CurrentStock:0.##} {inventoryItem.UnitOfMeasure}.");
            }
        }

        var unitCost = inventoryItem.WeightedAverageCostBdt;
        Guid? stockTransactionId = null;

        if (request.ActualQuantity > 0)
        {
            var txId = Guid.NewGuid();
            inventoryItem.DeductStock(request.ActualQuantity, txId);
            _inventoryItemRepository.Update(inventoryItem);

            var stockTx = new StockTransaction(
                id: txId,
                tenantId: entry.TenantId,
                farmId: entry.FarmId,
                inventoryItemId: inventoryItem.Id,
                transactionType: StockTransactionType.AutoConsumableUsage,
                quantity: request.ActualQuantity,
                unitCostBdt: unitCost,
                balanceAfter: inventoryItem.CurrentStock,
                transactionDate: entry.EntryDate,
                reason: $"Auto consumable deduction for entry {entry.Id}",
                referenceId: entry.Id);

            await _stockTransactionRepository.AddAsync(stockTx, cancellationToken);
            stockTransactionId = txId;
        }

        entry.Confirm(request.ActualQuantity, unitCost, stockTransactionId, request.AdjustmentReason);
        _entryRepository.Update(entry);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Confirmed daily consumable entry {EntryId} with quantity {Qty}, unit cost {UnitCost} BDT", 
                entry.Id, request.ActualQuantity, unitCost);
        }

        // Publish event for automated financial expense posting
        if (entry.TotalCostBdt.HasValue && entry.TotalCostBdt.Value > 0)
        {
            await _publisher.Publish(new DailyConsumableCostCalculatedEvent(
                Guid.NewGuid(),
                DateTime.UtcNow,
                entry.Id,
                entry.TenantId,
                entry.FarmId,
                entry.TotalCostBdt.Value,
                request.ActualQuantity,
                entry.EntryDate,
                inventoryItem.Id,
                inventoryItem.Name), cancellationToken);
        }
    }
}

public sealed record BulkConfirmDailyConsumableEntriesCommand(
    Guid FarmId,
    DateOnly EntryDate) : IRequest<int>;

public sealed class BulkConfirmDailyConsumableEntriesCommandHandler : IRequestHandler<BulkConfirmDailyConsumableEntriesCommand, int>
{
    private readonly IDailyConsumableEntryRepository _entryRepository;
    private readonly ISender _sender;
    private readonly ILogger<BulkConfirmDailyConsumableEntriesCommandHandler> _logger;

    public BulkConfirmDailyConsumableEntriesCommandHandler(
        IDailyConsumableEntryRepository entryRepository,
        ISender sender,
        ILogger<BulkConfirmDailyConsumableEntriesCommandHandler> logger)
    {
        _entryRepository = entryRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task<int> Handle(BulkConfirmDailyConsumableEntriesCommand request, CancellationToken cancellationToken)
    {
        var entries = await _entryRepository.GetByDateAsync(request.FarmId, request.EntryDate, cancellationToken);
        var pendingEntries = entries.Where(e => e.Status == DailyConsumableEntryStatus.Pending).ToList();

        int confirmedCount = 0;
        foreach (var entry in pendingEntries)
        {
            try
            {
                await _sender.Send(new ConfirmDailyConsumableEntryCommand(entry.Id, entry.ExpectedQuantity), cancellationToken);
                confirmedCount++;
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to bulk-confirm daily consumable entry {EntryId}", entry.Id);
            }
#pragma warning restore CA1031
        }

        return confirmedCount;
    }
}

public sealed record SkipDailyConsumableEntryCommand(
    Guid EntryId,
    string Reason) : IRequest;

public sealed class SkipDailyConsumableEntryCommandHandler : IRequestHandler<SkipDailyConsumableEntryCommand>
{
    private readonly IDailyConsumableEntryRepository _entryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SkipDailyConsumableEntryCommandHandler(
        IDailyConsumableEntryRepository entryRepository,
        IUnitOfWork unitOfWork)
    {
        _entryRepository = entryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SkipDailyConsumableEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(DailyConsumableEntry), request.EntryId);

        entry.Skip(request.Reason);
        _entryRepository.Update(entry);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
