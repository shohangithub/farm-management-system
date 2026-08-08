using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Farm360.Application.Common.Interfaces;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalSoldNotification(AnimalSoldEvent DomainEvent) : INotification;

public sealed class AnimalSoldEventHandler(
    IFinancialTransactionRepository repository,
    IUnitOfWork unitOfWork) : INotificationHandler<AnimalSoldNotification>
{
    public async Task Handle(AnimalSoldNotification notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        // Auto-create a financial transaction for the sale
        var transaction = FinancialTransaction.Create(
            tenantId: domainEvent.TenantId,
            farmId: domainEvent.FarmId,
            type: TransactionType.Income,
            category: TransactionCategory.LivestockSale,
            amountBdt: domainEvent.SalePriceBdt,
            transactionDate: domainEvent.SaleDate.ToDateTime(System.TimeOnly.MinValue), // Convert DateOnly to DateTime
            referenceId: domainEvent.AnimalId.ToString(),
            notes: $"Auto-generated transaction from sale of animal to {domainEvent.BuyerName ?? "Unknown"}"
        );

        await repository.AddAsync(transaction, cancellationToken);
        
        // Save changes using IUnitOfWork
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
