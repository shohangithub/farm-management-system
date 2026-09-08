using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Health.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed class VetVisitCreatedEventHandler : INotificationHandler<VetVisitCreatedEvent>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VetVisitCreatedEventHandler> _logger;

    public VetVisitCreatedEventHandler(
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<VetVisitCreatedEventHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(VetVisitCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.CostBdt.HasValue || notification.CostBdt.Value <= 0)
            return;

        var transaction = FinancialTransaction.Create(
            tenantId: notification.TenantId,
            farmId: notification.FarmId,
            type: TransactionType.Expense,
            category: TransactionCategory.VeterinaryCost,
            amountBdt: notification.CostBdt.Value,
            transactionDate: notification.VisitDate.ToDateTime(TimeOnly.MinValue),
            referenceId: notification.VetVisitId.ToString(),
            notes: $"Veterinary visit by Dr. {notification.VetName} ({notification.VisitType})",
            description: $"Vet Visit: {notification.VetName}"
        );

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted veterinary visit expense of {Cost} BDT for Visit {VisitId}", notification.CostBdt.Value, notification.VetVisitId);
        }
    }
}
