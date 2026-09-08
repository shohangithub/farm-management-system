using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record TreatmentLoggedNotification(TreatmentLoggedEvent DomainEvent, Guid FarmId) : INotification;

public sealed class TreatmentLoggedEventHandler : 
    INotificationHandler<TreatmentLoggedNotification>,
    INotificationHandler<TreatmentLoggedEvent>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TreatmentLoggedEventHandler> _logger;

    public TreatmentLoggedEventHandler(
        IFinancialTransactionRepository transactionRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork,
        ILogger<TreatmentLoggedEventHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task Handle(TreatmentLoggedNotification notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification.DomainEvent, notification.FarmId, cancellationToken);
    }

    public async Task Handle(TreatmentLoggedEvent notification, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(notification.AnimalId, cancellationToken);
        var farmId = animal?.FarmId ?? Guid.Empty;
        await ProcessEventAsync(notification, farmId, cancellationToken);
    }

    private async Task ProcessEventAsync(TreatmentLoggedEvent domainEvent, Guid farmId, CancellationToken cancellationToken)
    {
        if (domainEvent.CostBdt <= 0)
            return;

        if (farmId == Guid.Empty)
        {
            var animal = await _animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);
            farmId = animal?.FarmId ?? Guid.Empty;
        }

        if (farmId == Guid.Empty)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Cannot post treatment expense: FarmId could not be determined for Animal {AnimalId}", domainEvent.AnimalId);
            }
            return;
        }

        // 1. Post MedicineCost expense transaction to General Ledger
        var transaction = FinancialTransaction.Create(
            tenantId: domainEvent.TenantId,
            farmId: farmId,
            type: TransactionType.Expense,
            category: TransactionCategory.MedicineCost,
            amountBdt: domainEvent.CostBdt,
            transactionDate: domainEvent.StartDate.ToDateTime(TimeOnly.MinValue),
            referenceId: domainEvent.MedicalTreatmentId.ToString(),
            notes: $"Treatment: {domainEvent.MedicationName} for {domainEvent.Diagnosis}",
            description: $"Medical Treatment - Drug: {domainEvent.MedicationName}",
            animalId: domainEvent.AnimalId
        );

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // 2. Accumulate cost in AnimalCostLedger running total
        var ledger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (ledger == null)
        {
            ledger = AnimalCostLedger.Create(domainEvent.TenantId, domainEvent.AnimalId, farmId, 0m);
            ledger.RecordCost(TransactionCategory.MedicineCost, domainEvent.CostBdt);
            _ledgerRepository.Add(ledger);
        }
        else
        {
            ledger.RecordCost(TransactionCategory.MedicineCost, domainEvent.CostBdt);
            _ledgerRepository.Update(ledger);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted treatment expense of {Cost} BDT for Animal {AnimalId}", domainEvent.CostBdt, domainEvent.AnimalId);
        }
    }
}
