using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
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
    string? Notes = null,
    StockFundingSource FundingSource = StockFundingSource.CashPurchase,
    CreditTermsRequest? Credit = null,
    Guid? UnitId = null) : IRequest<Guid>;
// Quantity and UnitCostBdt are in UnitId (e.g. 4 "Bag (50 kg)" at ৳2,900 a bag); stock is kept in the item's base unit.

public sealed class RecordStockInCommandValidator : AbstractValidator<RecordStockInCommand>
{
    public RecordStockInCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCostBdt).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.FundingSource).IsInEnum();
        RuleFor(x => x.Credit)
            .Must(c => c is null)
            .When(x => x.FundingSource == StockFundingSource.OwnerContribution)
            .WithMessage("Owner-supplied stock is not bought, so it cannot be on credit.");
        RuleFor(x => x.Credit!.PaidNowBdt)
            .GreaterThanOrEqualTo(0)
            .LessThan(x => x.Quantity * x.UnitCostBdt)
            .When(x => x.Credit is not null)
            .WithMessage("The amount paid now must be less than the purchase total (otherwise it is a cash purchase).");
    }
}

public sealed class RecordStockInCommandHandler : IRequestHandler<RecordStockInCommand, Guid>
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly ITradeCreditService _credit;
    private readonly ISupplierRepository _suppliers;
    private readonly Farm360.Application.Inventory.Units.IUnitService _units;

    public RecordStockInCommandHandler(
        IInventoryItemRepository itemRepository,
        IStockTransactionRepository transactionRepository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILedgerPostingService ledgerPosting,
        ITradeCreditService credit,
        ISupplierRepository suppliers,
        Farm360.Application.Inventory.Units.IUnitService units)
    {
        _units = units;
        _ledgerPosting = ledgerPosting;
        _credit = credit;
        _suppliers = suppliers;
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

        // Entered in a pack unit (bags, bottles…): stock and average cost are kept in the base unit.
        var entered = await _units.ToBaseAsync(item, request.UnitId, request.Quantity, request.UnitCostBdt, cancellationToken);

        // 1. Receive stock on item (recalculates weighted average cost)
        item.ReceiveStock(entered.BaseQuantity, entered.BaseUnitCostBdt, transactionId);

        // 2. Log stock transaction
        var transaction = new StockTransaction(
            id: transactionId,
            tenantId: _tenantService.TenantId,
            farmId: request.FarmId,
            inventoryItemId: request.InventoryItemId,
            transactionType: StockTransactionType.StockIn,
            quantity: entered.BaseQuantity,
            unitCostBdt: entered.BaseUnitCostBdt,
            balanceAfter: item.CurrentStock,
            transactionDate: request.TransactionDate,
            supplierId: request.SupplierId,
            invoiceNumber: request.InvoiceNumber,
            batchNumber: request.BatchNumber,
            expiryDate: request.ExpiryDate,
            reason: request.Notes,
            recordedBy: _currentUserService.UserId?.ToString(),
            referenceId: null);

        transaction.SetEnteredUnit(entered.EnteredUnitName, entered.EnteredQuantity);

        _itemRepository.Update(item);
        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // 3. Book it: a paid purchase moves cash into the Inventory asset; owner-supplied or opening
        //    stock is owner capital. Either way it is an asset until used, not an expense.
        var posting = await _ledgerPosting.PostStockInAsync(
            item,
            request.Quantity * request.UnitCostBdt,
            request.TransactionDate,
            transactionId,
            request.FundingSource,
            request.InvoiceNumber ?? string.Empty,
            request.FundingSource == StockFundingSource.OwnerContribution
                ? $"Stock contributed - {item.Name}"
                : $"Stock purchase - {item.Name}" + (string.IsNullOrWhiteSpace(request.InvoiceNumber) ? string.Empty : $" (Inv {request.InvoiceNumber})"),
            cancellationToken);

        // 4. Bought on credit: the supplier is owed until paid (Cr Accounts Payable, not Cash).
        if (request.Credit is { } credit && posting is not null)
        {
            var supplier = request.SupplierId is { } supplierId
                ? await _suppliers.GetByIdAsync(supplierId, cancellationToken)
                : null;
            var party = string.IsNullOrWhiteSpace(credit.PartyName) ? supplier?.Name : credit.PartyName;
            if (string.IsNullOrWhiteSpace(party))
                throw new InvalidOperationException("Choose the supplier (or enter their name) for a purchase on credit.");

            await _credit.OpenAsync(posting, new CreditTerms(
                party, credit.PartyPhone ?? supplier?.Phone, credit.DueDate, credit.PaidNowBdt,
                credit.PaymentMethod, credit.Notes, supplier?.Id), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction.Id;
    }
}
