using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

/// <summary>What undoing a sale or death changed, for the confirmation message.</summary>
public sealed record ReverseAnimalExitResult(
    bool WasSold,
    DateOnly ExitDate,
    decimal ReceivedFromBuyerBdt,
    int MortalityRecordsRemoved);

/// <summary>
/// Undoes a sale or death recorded by mistake, in one transaction. The animal is back on the farm
/// (Active, in the pen/shed it left), and every module undoes its side:
///   - Finance: the sale revenue, cost of animal sold and mortality loss postings are reversed; a
///     credit sale's bill is cancelled and any amount received from the buyer is voided (the money
///     has to be returned outside the system); the cost ledger's sale revenue is cleared; labour
///     and overhead are re-split so the animal takes its share again.
///   - Health: the mortality record is removed; doses and treatments closed by the exit reopen.
///   - Feeding: plans ended by the exit run again and entries skipped or reversed by it go back to
///     Pending, to be confirmed again.
/// Shared (shed/batch) feed entries already re-divided without the animal are not re-divided back.
/// </summary>
public sealed record ReverseAnimalExitCommand(Guid AnimalId, string Reason)
    : IRequest<ReverseAnimalExitResult>, ITransactionalCommand;

public sealed class ReverseAnimalExitCommandValidator : AbstractValidator<ReverseAnimalExitCommand>
{
    public ReverseAnimalExitCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the sale or death is being undone.").MaximumLength(300);
    }
}

public sealed class ReverseAnimalExitCommandHandler(
    IAnimalRepository animals,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILivestockAccountingService livestockAccounting,
    IAnimalCostLedgerRepository ledgers,
    ITradeBillRepository bills,
    ITradeCreditService credit,
    IMortalityRecordRepository mortality,
    IPublisher publisher) : IRequestHandler<ReverseAnimalExitCommand, ReverseAnimalExitResult>
{
    public async Task<ReverseAnimalExitResult> Handle(ReverseAnimalExitCommand request, CancellationToken cancellationToken)
    {
        var animal = await animals.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var wasSold = animal.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Sold;
        var received = 0m;

        // A credit sale: the buyer's bill goes too. Payments taken against it are voided (the
        // money must be handed back); a written-off balance has to be undone by hand first.
        if (wasSold
            && await bills.FindBySourceAsync(animal.FarmId, animal.Id, TransactionCategory.AnimalSale, cancellationToken) is { IsCancelled: false } bill)
        {
            if (bill.AdjustedBdt > 0)
                throw new Farm360.Domain.Exceptions.DomainException(
                    $"Part of the buyer's balance for {animal.Tag.TagId} was written off or adjusted. Undo that in Payables & Receivables first.");

            foreach (var payment in await bills.GetPaymentsAsync(bill.Id, cancellationToken))
            {
                received += payment.AmountBdt;
                await credit.VoidPaymentAsync(bill, payment, cancellationToken);
            }

            await credit.CancelAsync(bill, cancellationToken);
        }

        animal.ReverseExit(currentUser.UserId ?? Guid.Empty, request.Reason);
        var reversed = animal.DomainEvents.OfType<AnimalExitReversedEvent>().Single();

        // Sale revenue / cost released / loss postings are all reversed for an animal back in the herd.
        await livestockAccounting.SyncDisposalAsync(animal, cancellationToken);

        if (await ledgers.GetByAnimalIdAsync(animal.Id, cancellationToken) is { } ledger)
        {
            ledger.RecordSaleRevenue(0m);
            ledgers.Update(ledger);
        }

        var removed = 0;
        if (!wasSold)
        {
            foreach (var record in await mortality.GetByAnimalIdAsync(animal.Id, cancellationToken))
            {
                mortality.Remove(record);
                removed++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Health, feeding and overhead undo their side of the exit.
        await publisher.Publish(reversed, cancellationToken);

        return new ReverseAnimalExitResult(wasSold, reversed.ExitDate, received, removed);
    }
}
