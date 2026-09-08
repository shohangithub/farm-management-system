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
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalAcquisitionPriceUpdatedNotification(AnimalAcquisitionPriceUpdatedEvent DomainEvent) : INotification;

public sealed class AnimalAcquisitionPriceUpdatedEventHandler : 
    INotificationHandler<AnimalAcquisitionPriceUpdatedNotification>,
    INotificationHandler<AnimalAcquisitionPriceUpdatedEvent>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnimalAcquisitionPriceUpdatedEventHandler> _logger;

    public AnimalAcquisitionPriceUpdatedEventHandler(
        IAnimalCostLedgerRepository ledgerRepository,
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<AnimalAcquisitionPriceUpdatedEventHandler> logger)
    {
        _ledgerRepository = ledgerRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task Handle(AnimalAcquisitionPriceUpdatedNotification notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification.DomainEvent, cancellationToken);
    }

    public Task Handle(AnimalAcquisitionPriceUpdatedEvent notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification, cancellationToken);
    }

    private async Task ProcessEventAsync(AnimalAcquisitionPriceUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        var newPrice = domainEvent.NewPriceBdt ?? 0m;

        // 1. Synchronize AnimalCostLedger acquisition cost bucket
        var ledger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (ledger != null)
        {
            ledger.UpdateAcquisitionCost(newPrice);
            _ledgerRepository.Update(ledger);
        }
        else
        {
            ledger = AnimalCostLedger.Create(
                domainEvent.TenantId,
                domainEvent.AnimalId,
                domainEvent.FarmId,
                newPrice
            );
            _ledgerRepository.Add(ledger);
        }

        // 2. Synchronize General Ledger FinancialTransaction for AnimalPurchase
        var existingTx = await _transactionRepository.GetAnimalPurchaseTransactionAsync(domainEvent.AnimalId, cancellationToken);

        if (domainEvent.NewPriceBdt.HasValue && domainEvent.NewPriceBdt.Value > 0)
        {
            if (existingTx != null)
            {
                existingTx.UpdateDetails(
                    TransactionCategory.AnimalPurchase,
                    domainEvent.NewPriceBdt.Value,
                    existingTx.TransactionDate,
                    $"Animal Purchase - Tag: {domainEvent.TagId}",
                    $"Acquisition cost for animal {domainEvent.TagId}",
                    animalId: domainEvent.AnimalId
                );
                await _transactionRepository.UpdateAsync(existingTx, cancellationToken);
            }
            else
            {
                var newTx = FinancialTransaction.Create(
                    tenantId: domainEvent.TenantId,
                    farmId: domainEvent.FarmId,
                    type: TransactionType.Expense,
                    category: TransactionCategory.AnimalPurchase,
                    amountBdt: domainEvent.NewPriceBdt.Value,
                    transactionDate: domainEvent.AcquisitionDate.ToDateTime(TimeOnly.MinValue),
                    referenceId: domainEvent.TagId,
                    notes: $"Acquisition cost for animal {domainEvent.TagId}",
                    description: $"Animal Purchase - Tag: {domainEvent.TagId}",
                    animalId: domainEvent.AnimalId,
                    isAutomated: true,
                    sourceModule: "Livestock"
                );
                await _transactionRepository.AddAsync(newTx, cancellationToken);
            }
        }
        else
        {
            // Purchase price was removed or set to 0; void/delete the transaction
            if (existingTx != null)
            {
                await _transactionRepository.DeleteAsync(existingTx, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Synchronized animal purchase price update for Animal {AnimalId} (Old: {OldPrice} BDT, New: {NewPrice} BDT)",
                domainEvent.AnimalId, domainEvent.OldPriceBdt, domainEvent.NewPriceBdt);
        }
    }
}
