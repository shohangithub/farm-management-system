using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.EventHandlers;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>
/// Confirming a feeding entry is all-or-nothing: the entry, stock deduction, cost and ledger commit
/// together (the event handlers run inside the command's transaction and never swallow failures),
/// and a second confirmation is refused instead of deducting twice.
/// </summary>
public class ConfirmFeedingAtomicityTests
{
    private readonly IDailyFeedingEntryRepository _entries = Substitute.For<IDailyFeedingEntryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private ConfirmDailyFeedingEntryCommandHandler Handler() => new(
        _entries, Substitute.For<IFeedFormulaRepository>(), Substitute.For<IFeedIngredientRepository>(),
        Substitute.For<IInventoryItemRepository>(), _unitOfWork, _publisher);

    private DailyFeedingEntry PendingEntry()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 17), Guid.NewGuid(), 5m, Guid.NewGuid());
        _entries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);
        return entry;
    }

    [Fact]
    public void ConfirmAndAdjust_AreTransactional()
    {
        new ConfirmDailyFeedingEntryCommand(Guid.NewGuid(), 1m).Should().BeAssignableTo<ITransactionalCommand>();
        new AdjustDailyFeedingEntryCommand(Guid.NewGuid(), 1m, null).Should().BeAssignableTo<ITransactionalCommand>();
        new RecordManualFeedingCommand(Guid.NewGuid(), new DateOnly(2026, 11, 17), [Guid.NewGuid()], Guid.NewGuid(), null, 1m)
            .Should().BeAssignableTo<ITransactionalCommand>();
    }

    [Fact]
    public async Task DoubleConfirm_IsAConflict_AndPublishesNothing()
    {
        var entry = PendingEntry();
        entry.Confirm(5m);
        entry.ClearDomainEvents();

        var act = () => Handler().Handle(new ConfirmDailyFeedingEntryCommand(entry.Id, 5m), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        await _publisher.DidNotReceiveWithAnyArgs().Publish(default(INotification)!, default);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StockHandlerFailure_PropagatesOutOfTheCommand_SoTheTransactionRollsBack()
    {
        var entry = PendingEntry();
        _publisher.Publish(Arg.Any<DailyEntryConfirmedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("stock deduction failed"));

        var act = () => Handler().Handle(new ConfirmDailyFeedingEntryCommand(entry.Id, 5m), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("stock deduction failed");
    }

    [Fact]
    public async Task ConfirmedEventHandler_RethrowsInsteadOfSwallowing()
    {
        var entryRepository = Substitute.For<IDailyFeedingEntryRepository>();
        entryRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));
        var handler = new DailyEntryConfirmedEventHandler(
            Substitute.For<IFeedFormulaRepository>(), Substitute.For<IFeedIngredientRepository>(), entryRepository,
            Substitute.For<IAnimalFeedingPlanRepository>(), Substitute.For<IAnimalFeedAllocationRepository>(),
            Substitute.For<IFeedAllocationService>(), Substitute.For<IInventoryItemRepository>(),
            Substitute.For<IStockTransactionRepository>(), _unitOfWork, _publisher,
            NullLogger<DailyEntryConfirmedEventHandler>.Instance);

        var act = () => handler.Handle(new DailyEntryConfirmedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5m, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TransactionBehavior_HandlerFailure_RollsBackAndNeverCommits()
    {
        var transaction = Substitute.For<ITransaction>();
        _unitOfWork.ExecuteStrategyAsync(Arg.Any<Func<Task<Unit>>>()).Returns(ci => ci.Arg<Func<Task<Unit>>>()());
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        var behavior = new TransactionBehavior<ConfirmDailyFeedingEntryCommand, Unit>(
            _unitOfWork, NullLogger<TransactionBehavior<ConfirmDailyFeedingEntryCommand, Unit>>.Instance);

        var act = () => behavior.Handle(new ConfirmDailyFeedingEntryCommand(Guid.NewGuid(), 5m),
            () => throw new InvalidOperationException("ledger posting failed"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackTransactionAsync(transaction, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<ITransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransactionBehavior_InsideAnOpenTransaction_JoinsIt()
    {
        _unitOfWork.HasActiveTransaction.Returns(true);
        var behavior = new TransactionBehavior<ConfirmDailyFeedingEntryCommand, Unit>(
            _unitOfWork, NullLogger<TransactionBehavior<ConfirmDailyFeedingEntryCommand, Unit>>.Instance);

        await behavior.Handle(new ConfirmDailyFeedingEntryCommand(Guid.NewGuid(), 5m), () => Task.FromResult(Unit.Value), CancellationToken.None);

        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }
}
