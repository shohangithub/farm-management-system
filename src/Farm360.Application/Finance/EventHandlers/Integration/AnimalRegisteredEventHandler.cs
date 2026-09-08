using System;
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

public sealed record AnimalRegisteredNotification(AnimalRegisteredEvent DomainEvent) : INotification;

public sealed class AnimalRegisteredEventHandler : 
    INotificationHandler<AnimalRegisteredNotification>,
    INotificationHandler<AnimalRegisteredEvent>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AnimalRegisteredEventHandler(
        IAnimalCostLedgerRepository ledgerRepository,
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _ledgerRepository = ledgerRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public Task Handle(AnimalRegisteredNotification notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification.DomainEvent, cancellationToken);
    }

    public Task Handle(AnimalRegisteredEvent notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification, cancellationToken);
    }

    private async Task ProcessEventAsync(AnimalRegisteredEvent domainEvent, CancellationToken cancellationToken)
    {
        // 1. Check if ledger already exists (idempotency check)
        var existingLedger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (existingLedger == null)
        {
            // 2. Create a new cost ledger for this animal
            var ledger = AnimalCostLedger.Create(
                domainEvent.TenantId,
                domainEvent.AnimalId,
                domainEvent.FarmId,
                domainEvent.AcquisitionPriceBdt ?? 0m
            );

            _ledgerRepository.Add(ledger);
        }

        // 3. If animal has acquisition price > 0, post AnimalPurchase expense to the General Ledger
        if (domainEvent.AcquisitionPriceBdt.HasValue && domainEvent.AcquisitionPriceBdt.Value > 0)
        {
            var transaction = FinancialTransaction.Create(
                tenantId: domainEvent.TenantId,
                farmId: domainEvent.FarmId,
                type: TransactionType.Expense,
                category: TransactionCategory.AnimalPurchase,
                amountBdt: domainEvent.AcquisitionPriceBdt.Value,
                transactionDate: DateTime.UtcNow,
                referenceId: domainEvent.TagId ?? domainEvent.AnimalId.ToString(),
                notes: $"Acquisition cost for animal {domainEvent.TagId}",
                description: $"Animal Purchase - Tag: {domainEvent.TagId}",
                animalId: domainEvent.AnimalId
            );

            await _transactionRepository.AddAsync(transaction, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
