using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Internal;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Events;
using Farm360.Domain.Finance.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Intelligence;

public class ManualExpenseLedgerPostingTests
{
    private readonly Guid _animalId = Guid.NewGuid();
    private readonly AnimalCostLedger _ledger;
    private readonly IFinancialTransactionRepository _transactions = Substitute.For<IFinancialTransactionRepository>();
    private readonly IAnimalCostLedgerRepository _ledgers = Substitute.For<IAnimalCostLedgerRepository>();
    private readonly FakeAllocations _allocations = new();
    private readonly AnimalCostLedgerUpdater _sut;

    public ManualExpenseLedgerPostingTests()
    {
        _ledger = AnimalCostLedger.Create(TestAnimals.TenantId, _animalId, TestAnimals.FarmId, 50_000m);
        _ledgers.GetByAnimalIdAsync(_animalId, Arg.Any<CancellationToken>()).Returns(_ledger);
        _sut = new AnimalCostLedgerUpdater(_transactions, _ledgers, _allocations, Substitute.For<IUnitOfWork>());
    }

    private FinancialTransactionCreatedEvent Record(FinancialTransaction t)
    {
        _transactions.GetByIdAsync(t.Id, Arg.Any<CancellationToken>()).Returns(t);
        return t.DomainEvents.OfType<FinancialTransactionCreatedEvent>().Single();
    }

    private FinancialTransaction Expense(TransactionCategory category, decimal amount, bool automated = false) =>
        FinancialTransaction.Create(TestAnimals.TenantId, TestAnimals.FarmId, TransactionType.Expense, category, amount,
            new DateTime(2025, 7, 1), animalId: _animalId, isAutomated: automated, sourceModule: automated ? "Health" : null);

    [Fact]
    public async Task Manual_vet_expense_reaches_the_vet_bucket()
    {
        await _sut.Handle(Record(Expense(TransactionCategory.VeterinaryCost, 2_500m)), CancellationToken.None);
        _ledger.TotalVetCostBdt.Should().Be(2_500m);
        _ledger.TotalCostBdt.Should().Be(52_500m);
    }

    [Fact]
    public async Task Automated_posting_is_left_to_its_dedicated_handler()
    {
        await _sut.Handle(Record(Expense(TransactionCategory.MedicineCost, 2_500m, automated: true)), CancellationToken.None);
        _ledger.TotalVetCostBdt.Should().Be(0m);
    }

    [Fact]
    public async Task Manual_labour_expense_is_posted_once_even_if_handled_twice()
    {
        var evt = Record(Expense(TransactionCategory.LaborCost, 3_000m));

        await _sut.Handle(evt, CancellationToken.None);
        await _sut.Handle(evt, CancellationToken.None);

        _allocations.Rows.Should().ContainSingle();
        _ledger.TotalLaborCostBdt.Should().Be(3_000m);
        _ledger.TotalCostBdt.Should().Be(53_000m);
    }

    [Fact]
    public async Task Expense_not_tagged_to_an_animal_is_ignored()
    {
        var t = FinancialTransaction.Create(TestAnimals.TenantId, TestAnimals.FarmId, TransactionType.Expense,
            TransactionCategory.LaborCost, 3_000m, new DateTime(2025, 7, 1));
        await _sut.Handle(Record(t), CancellationToken.None);
        _allocations.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Editing_a_manual_vet_expense_moves_the_amount_instead_of_leaving_the_old_one()
    {
        var t = Expense(TransactionCategory.VeterinaryCost, 2_500m);
        await _sut.Handle(Record(t), CancellationToken.None);

        t.UpdateDetails(TransactionCategory.VeterinaryCost, 1_000m, t.TransactionDate, t.Description, t.Notes, _animalId, null, null);
        await AnimalCostLedgerUpdater.SyncManualVetAsync(_ledgers, t, CancellationToken.None);

        _ledger.TotalVetCostBdt.Should().Be(1_000m);
        t.LedgerVetPostedBdt.Should().Be(1_000m);
    }

    [Fact]
    public async Task Moving_a_manual_vet_expense_to_another_animal_moves_the_cost()
    {
        var otherId = Guid.NewGuid();
        var other = AnimalCostLedger.Create(TestAnimals.TenantId, otherId, TestAnimals.FarmId, 0m);
        _ledgers.GetByAnimalIdAsync(otherId, Arg.Any<CancellationToken>()).Returns(other);

        var t = Expense(TransactionCategory.MedicineCost, 2_500m);
        await _sut.Handle(Record(t), CancellationToken.None);

        t.UpdateDetails(TransactionCategory.MedicineCost, 2_500m, t.TransactionDate, t.Description, t.Notes, otherId, null, null);
        await AnimalCostLedgerUpdater.SyncManualVetAsync(_ledgers, t, CancellationToken.None);

        _ledger.TotalVetCostBdt.Should().Be(0m);
        other.TotalVetCostBdt.Should().Be(2_500m);
    }

    [Fact]
    public async Task Deleting_a_manual_vet_expense_takes_it_off_the_ledger()
    {
        var t = Expense(TransactionCategory.VeterinaryCost, 2_500m);
        await _sut.Handle(Record(t), CancellationToken.None);

        await AnimalCostLedgerUpdater.SyncManualVetAsync(_ledgers, t, CancellationToken.None, removed: true);

        _ledger.TotalVetCostBdt.Should().Be(0m);
    }

    [Fact]
    public async Task Deleting_an_old_never_posted_vet_expense_subtracts_nothing()
    {
        _ledger.RecordCost(TransactionCategory.MedicineCost, 4_000m); // a real treatment
        var old = Expense(TransactionCategory.VeterinaryCost, 2_500m); // entered before postings existed

        await AnimalCostLedgerUpdater.SyncManualVetAsync(_ledgers, old, CancellationToken.None, removed: true);

        _ledger.TotalVetCostBdt.Should().Be(4_000m);
    }

    [Fact]
    public async Task Handling_the_created_event_twice_posts_the_vet_expense_once()
    {
        var evt = Record(Expense(TransactionCategory.VeterinaryCost, 2_500m));

        await _sut.Handle(evt, CancellationToken.None);
        await _sut.Handle(evt, CancellationToken.None);

        _ledger.TotalVetCostBdt.Should().Be(2_500m);
    }

    [Fact]
    public async Task Backfill_posts_old_expenses_once_and_rerunning_changes_nothing()
    {
        var vet = Expense(TransactionCategory.VeterinaryCost, 2_500m);
        var labour = Expense(TransactionCategory.LaborCost, 3_000m);
        var smallLabour = Expense(TransactionCategory.LaborCost, 0.01m);
        _transactions.GetAllByFarmIdAsync(TestAnimals.FarmId, Arg.Any<CancellationToken>())
            .Returns(new List<FinancialTransaction> { vet, labour, smallLabour });
        var handler = new Farm360.Application.Finance.Commands.BackfillManualAnimalExpensesCommandHandler(
            _transactions, _ledgers, _allocations, Substitute.For<IUnitOfWork>());
        var command = new Farm360.Application.Finance.Commands.BackfillManualAnimalExpensesCommand(TestAnimals.FarmId);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        _ledger.TotalVetCostBdt.Should().Be(2_500m);
        _ledger.TotalLaborCostBdt.Should().Be(3_000.01m);
        _allocations.Rows.Should().HaveCount(2);
        first.VetExpensesPosted.Should().Be(1);
        first.AllocationRowsWritten.Should().Be(2);
        second.VetExpensesPosted.Should().Be(0);
        second.AllocationRowsWritten.Should().Be(0);
    }

    [Fact]
    public async Task Editing_a_manual_labour_expense_rewrites_its_allocation_row()
    {
        var t = Expense(TransactionCategory.LaborCost, 3_000m);
        await _sut.Handle(Record(t), CancellationToken.None);

        t.UpdateDetails(TransactionCategory.LaborCost, 4_500m, t.TransactionDate, t.Description, t.Notes, _animalId, null, null);

        var animals = Substitute.For<Farm360.Domain.Livestock.Repositories.IAnimalRepository>();
        var rules = Substitute.For<IFarmBusinessRulesProvider>();
        rules.Current.Returns(new Farm360.Domain.BusinessRules.FarmBusinessRules());
        rules.For(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(new Farm360.Domain.BusinessRules.FarmBusinessRules());
        var service = new Farm360.Application.Finance.Services.OverheadAllocationService(
            _transactions, _allocations, _ledgers, animals, rules, Substitute.For<IUnitOfWork>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Farm360.Application.Finance.Services.OverheadAllocationService>.Instance);

        await service.ReallocateTransactionAsync(t.Id, CancellationToken.None);

        _allocations.Rows.Should().ContainSingle().Which.AllocatedAmountBdt.Should().Be(4_500m);
        _ledger.TotalLaborCostBdt.Should().Be(4_500m);
    }

    private sealed class FakeAllocations : IAnimalOverheadAllocationRepository
    {
        public List<AnimalOverheadAllocation> Rows { get; } = [];

        public void AddRange(IEnumerable<AnimalOverheadAllocation> allocations) => Rows.AddRange(allocations);

        public void RemoveRange(IEnumerable<AnimalOverheadAllocation> allocations)
        {
            foreach (var a in allocations.ToList())
            {
                Rows.Remove(a);
            }
        }

        public Task<HashSet<Guid>> GetAllocatedTransactionIdsAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Select(r => r.SourceTransactionId).Where(transactionIds.Contains).ToHashSet());

        public Task<IReadOnlyList<AnimalOverheadAllocation>> GetBySourceTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalOverheadAllocation>>(Rows.Where(r => r.SourceTransactionId == transactionId).ToList());

        public Task<AnimalOverheadTotals> GetTotalsForAnimalAsync(Guid animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Totals(animalId));

        public Task<IReadOnlyList<AnimalOverheadTotals>> GetTotalsForAnimalsAsync(IEnumerable<Guid> animalIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalOverheadTotals>>(animalIds.Select(Totals).ToList());

        public Task<IReadOnlyList<AnimalOverheadAllocation>> GetByAnimalAsync(Guid animalId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalOverheadAllocation>>(Rows.Where(r => r.AnimalId == animalId).ToList());

        public Task<IReadOnlyList<AnimalOverheadAllocation>> GetByFarmAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalOverheadAllocation>>(Rows.Where(r => r.FarmId == farmId && r.PeriodEnd >= from && r.PeriodStart <= to).ToList());

        private AnimalOverheadTotals Totals(Guid animalId) => new(
            animalId,
            Rows.Where(r => r.AnimalId == animalId && r.Bucket == OverheadBucket.Labour).Sum(r => r.AllocatedAmountBdt),
            Rows.Where(r => r.AnimalId == animalId && r.Bucket == OverheadBucket.Overhead).Sum(r => r.AllocatedAmountBdt));
    }
}
