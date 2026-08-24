using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.RecordStockWriteOff;

public class RecordStockWriteOffCommandHandler : IRequestHandler<RecordStockWriteOffCommand, Guid>
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RecordStockWriteOffCommandHandler(
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

    public async Task<Guid> Handle(RecordStockWriteOffCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant not identified.");

        var item = await _itemRepository.GetByIdAsync(request.InventoryItemId, cancellationToken);

        if (item == null || item.FarmId != request.FarmId)
            throw new InventoryDomainException($"Inventory item with ID {request.InventoryItemId} not found.");

        if (item.CurrentStock < request.Quantity)
            throw new InventoryDomainException($"Insufficient stock. Available: {item.CurrentStock}, Requested: {request.Quantity}");

        var transactionId = Guid.NewGuid();
        
        item.WriteOffStock(request.Quantity, request.Reason, transactionId);

        var transaction = new StockTransaction(
            transactionId,
            tenantId,
            request.FarmId,
            request.InventoryItemId,
            StockTransactionType.WriteOff,
            request.Quantity,
            item.WeightedAverageCostBdt,
            item.CurrentStock,
            request.TransactionDate,
            reason: request.Reason,
            recordedBy: _currentUserService.UserId?.ToString(),
            referenceId: null);

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        _itemRepository.Update(item);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction.Id;
    }
}
