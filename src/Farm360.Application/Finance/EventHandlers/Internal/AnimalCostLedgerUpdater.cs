using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance.Events;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Internal;

public sealed record FinancialTransactionCreatedNotification(FinancialTransactionCreatedEvent DomainEvent) : INotification;

public sealed class AnimalCostLedgerUpdater : INotificationHandler<FinancialTransactionCreatedNotification>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AnimalCostLedgerUpdater(IAnimalCostLedgerRepository ledgerRepository, IUnitOfWork unitOfWork)
    {
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(FinancialTransactionCreatedNotification notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        // Only process transactions linked to a specific animal
        if (!domainEvent.AnimalId.HasValue)
            return;

        var ledger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId.Value, cancellationToken);
        if (ledger == null)
            return; // Ledger should have been created when animal was registered

        // Accumulate the cost
        // If it's an expense, record it. If it's income (like sale), it's recorded separately when AnimalSoldEvent happens.
        // Or if it's animal sale, we can record sale revenue.
        if (domainEvent.Category == Domain.Finance.Enums.TransactionCategory.AnimalSale)
        {
            ledger.RecordSaleRevenue(domainEvent.AmountBdt);
        }
        else
        {
            // For MVP, we assume any other transaction linked to an animal is an accumulated cost
            ledger.RecordCost(domainEvent.Category, domainEvent.AmountBdt);
        }

        _ledgerRepository.Update(ledger);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
