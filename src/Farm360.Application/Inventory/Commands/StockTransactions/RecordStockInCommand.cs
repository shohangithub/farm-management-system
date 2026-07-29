using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.StockTransactions;

public sealed record RecordStockInCommand(
    Guid FarmId,
    Guid InventoryItemId,
    decimal Quantity,
    decimal UnitCostBdt,
    DateOnly TransactionDate,
    Guid? SupplierId = null,
    string? InvoiceNumber = null,
    string? BatchNumber = null,
    DateOnly? ExpiryDate = null,
    string? Notes = null) : IRequest<Guid>;

public sealed class RecordStockInCommandValidator : AbstractValidator<RecordStockInCommand>
{
    public RecordStockInCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCostBdt).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TransactionDate).NotEmpty();
    }
}

public sealed class RecordStockInCommandHandler : IRequestHandler<RecordStockInCommand, Guid>
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RecordStockInCommandHandler(
        IInventoryItemRepository itemRepository,
        IStockTransactionRepository transactionRepository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _itemRepository = itemRepository;
        _transactionRepository = transactionRepository;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RecordStockInCommand request, CancellationToken cancellationToken)
    {
        var item = await _itemRepository.GetByIdAsync(request.InventoryItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item with ID '{request.InventoryItemId}' was not found.");

        var transactionId = Guid.NewGuid();

        // 1. Receive stock on item (recalculates weighted average cost)
        item.ReceiveStock(request.Quantity, request.UnitCostBdt, transactionId);

        // 2. Log stock transaction
        var transaction = new StockTransaction(
            id: transactionId,
            tenantId: _tenantService.TenantId,
            farmId: request.FarmId,
            inventoryItemId: request.InventoryItemId,
            transactionType: StockTransactionType.StockIn,
            quantity: request.Quantity,
            unitCostBdt: request.UnitCostBdt,
            balanceAfter: item.CurrentStock,
            transactionDate: request.TransactionDate,
            supplierId: request.SupplierId,
            invoiceNumber: request.InvoiceNumber,
            batchNumber: request.BatchNumber,
            expiryDate: request.ExpiryDate,
            reason: request.Notes,
            recordedBy: _currentUserService.UserId?.ToString(),
            referenceId: null);

        _itemRepository.Update(item);
        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction.Id;
    }
}
