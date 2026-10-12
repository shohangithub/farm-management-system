using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Inventory.Commands.StockTransactions;

public sealed record RecordStockOutCommand(
    Guid FarmId,
    Guid InventoryItemId,
    decimal Quantity,
    StockTransactionType TransactionType,
    DateOnly TransactionDate,
    string? Reason = null,
    Guid? ReferenceId = null) : IRequest<Guid>;

public sealed class RecordStockOutCommandValidator : AbstractValidator<RecordStockOutCommand>
{
    public RecordStockOutCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.TransactionType).IsInEnum();
        RuleFor(x => x.TransactionDate).NotEmpty();
    }
}

public sealed class RecordStockOutCommandHandler : IRequestHandler<RecordStockOutCommand, Guid>
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerPostingService _ledgerPosting;

    public RecordStockOutCommandHandler(
        IInventoryItemRepository itemRepository,
        IStockTransactionRepository transactionRepository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILedgerPostingService ledgerPosting)
    {
        _ledgerPosting = ledgerPosting;
        _itemRepository = itemRepository;
        _transactionRepository = transactionRepository;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RecordStockOutCommand request, CancellationToken cancellationToken)
    {
        var item = await _itemRepository.GetByIdAsync(request.InventoryItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item with ID '{request.InventoryItemId}' was not found.");

        var transactionId = Guid.NewGuid();

        // Deduct stock
        item.DeductStock(request.Quantity, transactionId);

        var transaction = new StockTransaction(
            id: transactionId,
            tenantId: _tenantService.TenantId,
            farmId: request.FarmId,
            inventoryItemId: request.InventoryItemId,
            transactionType: request.TransactionType,
            quantity: request.Quantity,
            unitCostBdt: item.WeightedAverageCostBdt,
            balanceAfter: item.CurrentStock,
            transactionDate: request.TransactionDate,
            supplierId: null,
            invoiceNumber: null,
            batchNumber: null,
            expiryDate: null,
            reason: request.Reason,
            recordedBy: _currentUserService.UserId?.ToString(),
            referenceId: request.ReferenceId);

        _itemRepository.Update(item);
        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // Stock used (or written off) leaves the Inventory asset and becomes a cost at average cost.
        var writeOff = request.TransactionType == StockTransactionType.WriteOff;
        await _ledgerPosting.PostConsumptionAsync(
            item,
            request.Quantity,
            item.WeightedAverageCostBdt,
            request.TransactionDate,
            transactionId,
            writeOff ? $"Stock written off - {item.Name}" : $"Stock used - {item.Name}",
            category: writeOff ? Farm360.Domain.Finance.Enums.TransactionCategory.InventoryWriteOff : null,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction.Id;
    }
}
