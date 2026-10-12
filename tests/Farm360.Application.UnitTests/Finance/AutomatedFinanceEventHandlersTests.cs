using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Integration;
using Farm360.Application.Finance.Services;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>
/// The automated postings that feed the books, run against an in-memory ledger so the accounting
/// rules (settlement, idempotency, cost release) are tested — not just that a mock was called.
/// </summary>
public class AutomatedFinanceEventHandlersTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    private readonly InMemoryFinancialTransactionRepository _ledger = new();
    private readonly IAnimalCostLedgerRepository _costLedgers = Substitute.For<IAnimalCostLedgerRepository>();
    private readonly IInventoryItemRepository _inventory = Substitute.For<IInventoryItemRepository>();
    private readonly IStockTransactionRepository _stockTransactions = Substitute.For<IStockTransactionRepository>();
    private readonly IAnimalRepository _animals = Substitute.For<IAnimalRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private LedgerPostingService Posting => new(_ledger);

    private LivestockAccountingService Livestock => new(Posting, _ledger);

    private Animal RegisterAnimal(decimal? price, DateOnly acquired, AcquisitionType type = AcquisitionType.Purchased)
    {
        var animal = Animal.Create(TenantId, FarmId, AnimalTag.Create($"BD-{Guid.NewGuid():N}"[..10], TagType.Manual),
            AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male, acquired.AddYears(-1), type, acquired, price, null);
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        return animal;
    }

    // ── Feeding ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FeedCost_FromStock_IsSettledAgainstInventory_AndUntrackedPartIsCash()
    {
        var handler = new DailyEntryConfirmedFinanceEventHandler(Posting, _costLedgers, _unitOfWork,
            Substitute.For<ILogger<DailyEntryConfirmedFinanceEventHandler>>());
        var evt = new DailyFeedingCostCalculatedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), TenantId, FarmId,
            TotalCostBdt: 450.50m, ActualKg: 15m, EntryDate: new DateOnly(2026, 9, 8),
            AnimalId: null, BatchId: null, ShedId: null, StockCostBdt: 300m);

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None); // re-confirmation must not double-post

        var feed = _ledger.Of(TransactionCategory.FeedCost);
        feed.Should().HaveCount(2);
        feed.Single(t => t.Settlement == SettlementAccount.Inventory).AmountBdt.Should().Be(300m);
        feed.Single(t => t.Settlement == SettlementAccount.Cash).AmountBdt.Should().Be(150.50m);
        feed.Should().OnlyContain(t => t.IsAutomated && t.SourceModule == "Feeding" && t.Type == TransactionType.Expense);

        // P&L sees the full cost; the cash book only the untracked part.
        AccountChart.Expenses(_ledger.Rows).Should().Be(450.50m);
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(-150.50m);
    }

    // ── Vaccination ─────────────────────────────────────────────────────────

    private InventoryItem Vaccine(decimal stock) =>
        new(Guid.NewGuid(), TenantId, FarmId, "Anthrax Vaccine", "ANT-001", InventoryCategory.Vaccine, "vial", 10m,
            initialStock: stock, initialCostBdt: 120m, storageLocation: "WH-1");

    private VaccinationAdministeredStockDeductionHandler VaccinationStockHandler() =>
        new(_inventory, _stockTransactions, Posting, _unitOfWork, Substitute.For<ILogger<VaccinationAdministeredStockDeductionHandler>>());

    [Fact]
    public async Task Vaccination_FromStock_DeductsStock_AndBooksMedicineAgainstInventory()
    {
        var item = Vaccine(100m);
        _inventory.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        var evt = new VaccinationAdministeredEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), TenantId, Guid.NewGuid(),
            "Anthrax Vaccine", new DateOnly(2026, 9, 8), Guid.NewGuid(), item.Id, 2.5m);

        await VaccinationStockHandler().Handle(evt, CancellationToken.None);

        item.CurrentStock.Should().Be(97.5m);
        var posting = _ledger.Of(TransactionCategory.MedicineCost).Should().ContainSingle().Subject;
        posting.AmountBdt.Should().Be(300m);
        posting.Settlement.Should().Be(SettlementAccount.Inventory);
        posting.SourceDocumentId.Should().Be(evt.VaccinationEventId);
    }

    [Fact]
    public async Task Vaccination_NotInStock_IsBookedAsCash_AndStockUntouched()
    {
        var item = Vaccine(1m);
        _inventory.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        var evt = new VaccinationAdministeredEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), TenantId, Guid.NewGuid(),
            "Anthrax Vaccine", new DateOnly(2026, 9, 8), Guid.NewGuid(), item.Id, 2.5m);

        await VaccinationStockHandler().Handle(evt, CancellationToken.None);

        item.CurrentStock.Should().Be(1m);
        _ledger.Of(TransactionCategory.MedicineCost).Should().ContainSingle()
            .Which.Settlement.Should().Be(SettlementAccount.Cash);
    }

    [Fact]
    public async Task VaccinationFinanceHandler_OnlyUpdatesCostLedger_NeverPostsToTheBooks()
    {
        var item = Vaccine(100m);
        _inventory.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        var handler = new VaccinationAdministeredFinanceEventHandler(_costLedgers, _inventory, _animals, _unitOfWork);
        var evt = new VaccinationAdministeredEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), TenantId, Guid.NewGuid(),
            "Anthrax Vaccine", new DateOnly(2026, 9, 8), Guid.NewGuid(), item.Id, 2.5m);

        await handler.Handle(evt, CancellationToken.None);

        _ledger.Rows.Should().BeEmpty();
        _costLedgers.Received(1).Add(Arg.Is<AnimalCostLedger>(l => l.TotalVetCostBdt == 300m));
    }

    // ── Livestock ───────────────────────────────────────────────────────────

    private AnimalAcquisitionPriceUpdatedEventHandler PriceHandler() =>
        new(_costLedgers, _animals, Livestock, _unitOfWork, Substitute.For<ILogger<AnimalAcquisitionPriceUpdatedEventHandler>>());

    private static AnimalAcquisitionPriceUpdatedEvent PriceChanged(Animal animal, decimal? oldPrice, decimal? newPrice) =>
        new(Guid.NewGuid(), DateTime.UtcNow, animal.Id, TenantId, FarmId, animal.Tag.TagId, oldPrice, newPrice, animal.AcquisitionDate);

    [Fact]
    public async Task PriceUpdate_RevisesLegacyPurchase_ToNewAmountAndAcquisitionDate_WithoutDuplicating()
    {
        var acquired = new DateOnly(2026, 9, 8);
        var animal = RegisterAnimal(65000m, acquired);
        var ledger = AnimalCostLedger.Create(TenantId, animal.Id, FarmId, 50000m);
        _costLedgers.GetByAnimalIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(ledger);

        // Posted by the old code: dated on registration day, no source link.
        var legacy = FinancialTransaction.Create(TenantId, FarmId, TransactionType.Expense, TransactionCategory.AnimalPurchase,
            50000m, new DateTime(2026, 10, 1), referenceId: animal.Tag.TagId, animalId: animal.Id, isAutomated: true, sourceModule: "Livestock");
        await _ledger.AddAsync(legacy);

        await PriceHandler().Handle(PriceChanged(animal, 50000m, 65000m), CancellationToken.None);

        ledger.AcquisitionCostBdt.Should().Be(65000m);
        var purchase = _ledger.Of(TransactionCategory.AnimalPurchase).Should().ContainSingle().Subject;
        purchase.Should().BeSameAs(legacy);
        purchase.AmountBdt.Should().Be(65000m);
        purchase.TransactionDate.Should().Be(acquired.ToDateTime(TimeOnly.MinValue));
        purchase.SourceDocumentId.Should().Be(animal.Id);
    }

    [Fact]
    public async Task PriceAdded_PostsPurchaseAsLivestockAsset_NotAnExpense()
    {
        var animal = RegisterAnimal(75000m, new DateOnly(2026, 9, 8));

        await PriceHandler().Handle(PriceChanged(animal, null, 75000m), CancellationToken.None);

        var purchase = _ledger.Of(TransactionCategory.AnimalPurchase).Should().ContainSingle().Subject;
        purchase.AmountBdt.Should().Be(75000m);
        purchase.IsAutomated.Should().BeTrue();
        AccountChart.Expenses(_ledger.Rows).Should().Be(0m, "buying an animal is an asset, not a cost of the period");
        AccountChart.Balances(_ledger.Rows)[AccountChart.LivestockAccount].Should().Be(75000m);
    }

    [Fact]
    public async Task PriceRemoved_RemovesThePurchasePosting()
    {
        var animal = RegisterAnimal(null, new DateOnly(2026, 9, 8));
        await _ledger.AddAsync(FinancialTransaction.Create(TenantId, FarmId, TransactionType.Expense, TransactionCategory.AnimalPurchase,
            50000m, DateTime.UtcNow, animalId: animal.Id, isAutomated: true, sourceModule: "Livestock", sourceDocumentId: animal.Id));

        await PriceHandler().Handle(PriceChanged(animal, 50000m, null), CancellationToken.None);

        _ledger.Of(TransactionCategory.AnimalPurchase).Should().BeEmpty();
    }

    [Fact]
    public async Task Sale_BooksRevenue_AndReleasesPurchaseCost_SoProfitIsTheMargin()
    {
        var animal = RegisterAnimal(50000m, new DateOnly(2026, 6, 1));
        await Livestock.SyncPurchaseAsync(animal);
        animal.Sell(72000m, new DateOnly(2026, 10, 1), Guid.NewGuid(), "Karim", 310m);

        var handler = new AnimalSoldEventHandler(_animals, Livestock, _costLedgers, _unitOfWork);
        var sold = animal.DomainEvents.OfType<AnimalSoldEvent>().Single();
        await handler.Handle(sold, CancellationToken.None);
        await handler.Handle(sold, CancellationToken.None); // idempotent

        _ledger.Of(TransactionCategory.AnimalSale).Should().ContainSingle().Which.AmountBdt.Should().Be(72000m);
        var cogs = _ledger.Of(TransactionCategory.CostOfAnimalsSold).Should().ContainSingle().Subject;
        cogs.AmountBdt.Should().Be(50000m);
        cogs.Settlement.Should().Be(SettlementAccount.Livestock);

        AccountChart.NetProfit(_ledger.Rows).Should().Be(22000m);
        AccountChart.Balances(_ledger.Rows).GetValueOrDefault(AccountChart.LivestockAccount).Should().Be(0m);
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(22000m);
    }

    [Fact]
    public async Task Death_WritesTheAnimalOffTheLivestockAsset_AsAMortalityLoss()
    {
        var animal = RegisterAnimal(40000m, new DateOnly(2026, 6, 1));
        await Livestock.SyncPurchaseAsync(animal);
        animal.RecordDeath(DisposalReason.Disease, new DateOnly(2026, 9, 20), null);

        var handler = new AnimalDiedFinanceEventHandler(_animals, Livestock, _unitOfWork);
        await handler.Handle(animal.DomainEvents.OfType<AnimalDiedEvent>().Single(), CancellationToken.None);

        var loss = _ledger.Of(TransactionCategory.LivestockLoss).Should().ContainSingle().Subject;
        loss.AmountBdt.Should().Be(40000m);
        loss.TransactionDate.Should().Be(new DateTime(2026, 9, 20));
        AccountChart.NetProfit(_ledger.Rows).Should().Be(-40000m);
        AccountChart.Balances(_ledger.Rows).GetValueOrDefault(AccountChart.LivestockAccount).Should().Be(0m);
    }

    [Fact]
    public async Task BornOnFarm_HasNoPurchasePosting()
    {
        var animal = RegisterAnimal(null, new DateOnly(2026, 6, 1), AcquisitionType.BornOnFarm);

        await Livestock.SyncPurchaseAsync(animal);

        _ledger.Rows.Should().BeEmpty();
    }
}
