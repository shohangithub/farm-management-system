using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Health.Events;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>Books a vet visit fee paid in cash. Keyed on the visit, so it cannot be posted twice.</summary>
public sealed class VetVisitCreatedEventHandler : INotificationHandler<VetVisitCreatedEvent>
{
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;

    public VetVisitCreatedEventHandler(ILedgerPostingService ledgerPosting, IUnitOfWork unitOfWork)
    {
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(VetVisitCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.CostBdt.HasValue || notification.CostBdt.Value <= 0)
            return;

        await _ledgerPosting.PostAsync(new LedgerPosting(
            notification.TenantId,
            notification.FarmId,
            TransactionCategory.VeterinaryCost,
            notification.CostBdt.Value,
            notification.VisitDate.ToDateTime(TimeOnly.MinValue),
            notification.VetVisitId,
            "Health",
            $"Vet Visit: {notification.VetName}",
            Notes: $"Veterinary visit by Dr. {notification.VetName} ({notification.VisitType})"), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
