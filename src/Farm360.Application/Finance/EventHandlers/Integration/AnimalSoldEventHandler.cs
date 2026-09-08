using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalSoldNotification(AnimalSoldEvent DomainEvent) : INotification;

public sealed class AnimalSoldEventHandler(
    IFinancialTransactionRepository repository,
    IAnimalCostLedgerRepository ledgerRepository,
    IUnitOfWork unitOfWork) : 
    INotificationHandler<AnimalSoldNotification>,
    INotificationHandler<AnimalSoldEvent>
{
    public Task Handle(AnimalSoldNotification notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification.DomainEvent, cancellationToken);
    }

    public Task Handle(AnimalSoldEvent notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification, cancellationToken);
    }

    private async Task ProcessEventAsync(AnimalSoldEvent domainEvent, CancellationToken cancellationToken)
    {
        // 1. Auto-create a financial transaction for the sale
        var transaction = FinancialTransaction.Create(
            tenantId: domainEvent.TenantId,
            farmId: domainEvent.FarmId,
            type: TransactionType.Income,
            category: TransactionCategory.AnimalSale,
            amountBdt: domainEvent.SalePriceBdt,
            transactionDate: domainEvent.SaleDate.ToDateTime(System.TimeOnly.MinValue),
            referenceId: domainEvent.AnimalId.ToString(),
            notes: $"Auto-generated transaction from sale of animal to {domainEvent.BuyerName ?? "Unknown"}",
            description: $"Sale of Animal to {domainEvent.BuyerName ?? "Buyer"}",
            animalId: domainEvent.AnimalId,
            isAutomated: true,
            sourceModule: "Livestock"
        );

        await repository.AddAsync(transaction, cancellationToken);

        // 2. Update AnimalCostLedger with the realized sale revenue
        var ledger = await ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (ledger != null)
        {
            ledger.RecordSaleRevenue(domainEvent.SalePriceBdt);
            ledgerRepository.Update(ledger);
        }

        // 3. Save changes using IUnitOfWork
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
