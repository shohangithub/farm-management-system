using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseOrders;

/// <param name="Credit">Set when the goods were received on credit — the supplier is owed until paid.</param>
/// <param name="MoneyAccountId">Paid in cash: the account it was paid from (null = the farm's default).</param>
/// <param name="PaidByPartnerId">Paid directly by a partner out of their own pocket (books their capital).</param>
public record FulfillPurchaseOrderCommand(Guid Id, CreditTermsRequest? Credit = null, Guid? MoneyAccountId = null, Guid? PaidByPartnerId = null)
    : IRequest<Unit>, ITransactionalCommand;

public class FulfillPurchaseOrderCommandHandler : IRequestHandler<FulfillPurchaseOrderCommand, Unit>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ITradeCreditService _credit;
    private readonly ISupplierRepository _suppliers;
    private readonly IMoneyAccountService? _money;

    public FulfillPurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ITradeCreditService credit,
        ISupplierRepository suppliers,
        IMoneyAccountService? money = null)
    {
        _money = money;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _credit = credit;
        _suppliers = suppliers;
    }

    public async Task<Unit> Handle(FulfillPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        // Fulfilling the PO triggers the PurchaseOrderFulfilledEvent, 
        // which will be handled by an event handler to increase stock and post expense.
        purchaseOrder.Fulfill();

        var domainEvents = purchaseOrder.DomainEvents.OfType<Farm360.Domain.Inventory.Events.PurchaseOrderFulfilledEvent>().ToList();

        await _repository.UpdateAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(new Farm360.Application.Inventory.EventHandlers.PurchaseOrderFulfilledNotification(domainEvent), cancellationToken);
        }

        // Received on credit: the stock purchase just posted is owed to the supplier, not paid.
        if (request.Credit is not null && request.PaidByPartnerId is not null)
            throw new InvalidOperationException("Choose one: on credit, or paid directly by a partner.");

        if (request.Credit is { } terms)
        {
            var supplier = await _suppliers.GetByIdAsync(purchaseOrder.SupplierId, cancellationToken);
            var party = string.IsNullOrWhiteSpace(terms.PartyName) ? supplier?.Name ?? "Supplier" : terms.PartyName;
            await _credit.OpenForSourceAsync(purchaseOrder.FarmId, purchaseOrder.Id, TransactionCategory.InventoryPurchase,
                new CreditTerms(party, terms.PartyPhone ?? supplier?.Phone, terms.DueDate, terms.PaidNowBdt, terms.PaymentMethod,
                    terms.Notes, purchaseOrder.SupplierId, terms.MoneyAccountId ?? request.MoneyAccountId),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (_money is not null)
        {
            // Paid on receipt: from the chosen account, or directly by a partner.
            await _money.ApplyToSourceAsync(purchaseOrder.FarmId, purchaseOrder.Id, TransactionCategory.InventoryPurchase,
                new PaymentSource(request.MoneyAccountId, request.PaidByPartnerId), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        
        return Unit.Value;
    }
}
