using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

public sealed record ConfirmDailyFeedingEntryCommand(
    Guid EntryId,
    decimal ActualKg,
    Guid? InventoryTransactionId = null,
    string? AdjustmentReason = null) : IRequest;

public sealed class ConfirmDailyFeedingEntryCommandValidator : AbstractValidator<ConfirmDailyFeedingEntryCommand>
{
    public ConfirmDailyFeedingEntryCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.ActualKg).GreaterThanOrEqualTo(0);
    }
}

public sealed class ConfirmDailyFeedingEntryCommandHandler : IRequestHandler<ConfirmDailyFeedingEntryCommand>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ConfirmDailyFeedingEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new InvalidOperationException("Daily feeding entry not found.");

        entry.Confirm(request.ActualKg, request.InventoryTransactionId, request.AdjustmentReason);

        _repository.Update(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
