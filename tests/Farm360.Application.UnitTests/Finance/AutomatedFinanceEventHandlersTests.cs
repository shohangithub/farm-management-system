using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Integration;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

public class AutomatedFinanceEventHandlersTests
{
    private readonly IFinancialTransactionRepository _transactionRepository = Substitute.For<IFinancialTransactionRepository>();
    private readonly IAnimalCostLedgerRepository _ledgerRepository = Substitute.For<IAnimalCostLedgerRepository>();
    private readonly IInventoryItemRepository _inventoryRepository = Substitute.For<IInventoryItemRepository>();
    private readonly IAnimalRepository _animalRepository = Substitute.For<IAnimalRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task DailyEntryConfirmedFinanceEventHandler_ShouldPostFeedCost_AndMarkAutomated()
    {
        // Arrange
        var logger = Substitute.For<ILogger<DailyEntryConfirmedFinanceEventHandler>>();
        var handler = new DailyEntryConfirmedFinanceEventHandler(
            _transactionRepository,
            _ledgerRepository,
            _unitOfWork,
            logger);

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        var evt = new DailyFeedingCostCalculatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            entryId,
            tenantId,
            farmId,
            TotalCostBdt: 450.50m,
            ActualKg: 15.0m,
            EntryDate: new DateOnly(2026, 9, 8),
            AnimalId: animalId,
            BatchId: null,
            ShedId: null);

        FinancialTransaction? capturedTx = null;
        await _transactionRepository.AddAsync(Arg.Do<FinancialTransaction>(t => capturedTx = t), Arg.Any<CancellationToken>());

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        capturedTx.Should().NotBeNull();
        capturedTx!.AmountBdt.Should().Be(450.50m);
        capturedTx.Type.Should().Be(TransactionType.Expense);
        capturedTx.Category.Should().Be(TransactionCategory.FeedCost);
        capturedTx.IsAutomated.Should().BeTrue();
        capturedTx.SourceModule.Should().Be("Feeding");
        capturedTx.AnimalId.Should().Be(animalId);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VaccinationAdministeredFinanceEventHandler_ShouldPostMedicineCost_AndMarkAutomated()
    {
        // Arrange
        var logger = Substitute.For<ILogger<VaccinationAdministeredFinanceEventHandler>>();
        var handler = new VaccinationAdministeredFinanceEventHandler(
            _transactionRepository,
            _ledgerRepository,
            _inventoryRepository,
            _animalRepository,
            _unitOfWork,
            logger);

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var item = new InventoryItem(
            itemId,
            tenantId,
            farmId,
            "Anthrax Vaccine",
            "ANT-001",
            InventoryCategory.Vaccine,
            "vial",
            10m,
            initialStock: 100m,
            initialCostBdt: 120m,
            storageLocation: "WH-1");

        _inventoryRepository.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(item);

        var evt = new VaccinationAdministeredEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            tenantId,
            animalId,
            "Anthrax Vaccine",
            new DateOnly(2026, 9, 8),
            Guid.NewGuid(),
            InventoryItemId: itemId,
            DosageQuantity: 2.5m);

        FinancialTransaction? capturedTx = null;
        await _transactionRepository.AddAsync(Arg.Do<FinancialTransaction>(t => capturedTx = t), Arg.Any<CancellationToken>());

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert: 2.5 * 120 = 300 BDT
        capturedTx.Should().NotBeNull();
        capturedTx!.AmountBdt.Should().Be(300m);
        capturedTx.Type.Should().Be(TransactionType.Expense);
        capturedTx.Category.Should().Be(TransactionCategory.MedicineCost);
        capturedTx.IsAutomated.Should().BeTrue();
        capturedTx.SourceModule.Should().Be("Health");
        capturedTx.AnimalId.Should().Be(animalId);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StockWriteOffFinanceEventHandler_ShouldPostMiscellaneousExpense_AndMarkAutomated()
    {
        // Arrange
        var logger = Substitute.For<ILogger<StockWriteOffFinanceEventHandler>>();
        var handler = new StockWriteOffFinanceEventHandler(
            _transactionRepository,
            _unitOfWork,
            logger);

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var txId = Guid.NewGuid();

        var evt = new StockWriteOffEvent(
            txId,
            itemId,
            tenantId,
            farmId,
            WriteOffQuantity: 5m,
            Reason: "Spoilage due to moisture",
            UnitCostBdt: 85m);

        FinancialTransaction? capturedTx = null;
        await _transactionRepository.AddAsync(Arg.Do<FinancialTransaction>(t => capturedTx = t), Arg.Any<CancellationToken>());

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert: 5 * 85 = 425 BDT
        capturedTx.Should().NotBeNull();
        capturedTx!.AmountBdt.Should().Be(425m);
        capturedTx.Type.Should().Be(TransactionType.Expense);
        capturedTx.Category.Should().Be(TransactionCategory.MiscellaneousExpense);
        capturedTx.IsAutomated.Should().BeTrue();
        capturedTx.SourceModule.Should().Be("Inventory");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnimalAcquisitionPriceUpdatedEventHandler_WhenPriceUpdated_ShouldUpdateExistingTransactionAndLedger()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AnimalAcquisitionPriceUpdatedEventHandler>>();
        var handler = new AnimalAcquisitionPriceUpdatedEventHandler(
            _ledgerRepository,
            _transactionRepository,
            _unitOfWork,
            logger);

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        var ledger = AnimalCostLedger.Create(tenantId, animalId, farmId, 50000m);
        _ledgerRepository.GetByAnimalIdAsync(animalId, Arg.Any<CancellationToken>())
            .Returns(ledger);

        var existingTx = FinancialTransaction.Create(
            tenantId,
            farmId,
            TransactionType.Expense,
            TransactionCategory.AnimalPurchase,
            50000m,
            DateTime.UtcNow,
            referenceId: "TAG-101",
            animalId: animalId,
            isAutomated: true,
            sourceModule: "Livestock");

        _transactionRepository.GetAnimalPurchaseTransactionAsync(animalId, Arg.Any<CancellationToken>())
            .Returns(existingTx);

        var evt = new AnimalAcquisitionPriceUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            animalId,
            tenantId,
            farmId,
            "TAG-101",
            OldPriceBdt: 50000m,
            NewPriceBdt: 65000m,
            AcquisitionDate: new DateOnly(2026, 9, 8));

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert: Ledger should be 65000
        ledger.AcquisitionCostBdt.Should().Be(65000m);
        _ledgerRepository.Received(1).Update(ledger);

        // Assert: Existing transaction amount should be updated to 65000
        existingTx.AmountBdt.Should().Be(65000m);
        await _transactionRepository.Received(1).UpdateAsync(existingTx, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnimalAcquisitionPriceUpdatedEventHandler_WhenPriceAdded_ShouldCreateNewTransactionAndLedger()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AnimalAcquisitionPriceUpdatedEventHandler>>();
        var handler = new AnimalAcquisitionPriceUpdatedEventHandler(
            _ledgerRepository,
            _transactionRepository,
            _unitOfWork,
            logger);

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        _ledgerRepository.GetByAnimalIdAsync(animalId, Arg.Any<CancellationToken>())
            .Returns((AnimalCostLedger?)null);

        _transactionRepository.GetAnimalPurchaseTransactionAsync(animalId, Arg.Any<CancellationToken>())
            .Returns((FinancialTransaction?)null);

        FinancialTransaction? capturedTx = null;
        await _transactionRepository.AddAsync(Arg.Do<FinancialTransaction>(t => capturedTx = t), Arg.Any<CancellationToken>());

        var evt = new AnimalAcquisitionPriceUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            animalId,
            tenantId,
            farmId,
            "TAG-202",
            OldPriceBdt: null,
            NewPriceBdt: 75000m,
            AcquisitionDate: new DateOnly(2026, 9, 8));

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert: Ledger created with 75000
        _ledgerRepository.Received(1).Add(Arg.Is<AnimalCostLedger>(l => l.AcquisitionCostBdt == 75000m && l.AnimalId == animalId));

        // Assert: New transaction created with 75000
        capturedTx.Should().NotBeNull();
        capturedTx!.AmountBdt.Should().Be(75000m);
        capturedTx.Category.Should().Be(TransactionCategory.AnimalPurchase);
        capturedTx.Type.Should().Be(TransactionType.Expense);
        capturedTx.IsAutomated.Should().BeTrue();
        capturedTx.SourceModule.Should().Be("Livestock");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnimalAcquisitionPriceUpdatedEventHandler_WhenPriceRemoved_ShouldDeleteExistingTransaction()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AnimalAcquisitionPriceUpdatedEventHandler>>();
        var handler = new AnimalAcquisitionPriceUpdatedEventHandler(
            _ledgerRepository,
            _transactionRepository,
            _unitOfWork,
            logger);

        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        var ledger = AnimalCostLedger.Create(tenantId, animalId, farmId, 50000m);
        _ledgerRepository.GetByAnimalIdAsync(animalId, Arg.Any<CancellationToken>())
            .Returns(ledger);

        var existingTx = FinancialTransaction.Create(
            tenantId,
            farmId,
            TransactionType.Expense,
            TransactionCategory.AnimalPurchase,
            50000m,
            DateTime.UtcNow,
            referenceId: "TAG-303",
            animalId: animalId,
            isAutomated: true,
            sourceModule: "Livestock");

        _transactionRepository.GetAnimalPurchaseTransactionAsync(animalId, Arg.Any<CancellationToken>())
            .Returns(existingTx);

        var evt = new AnimalAcquisitionPriceUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            animalId,
            tenantId,
            farmId,
            "TAG-303",
            OldPriceBdt: 50000m,
            NewPriceBdt: null,
            AcquisitionDate: new DateOnly(2026, 9, 8));

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert: Ledger should reset to 0
        ledger.AcquisitionCostBdt.Should().Be(0m);
        _ledgerRepository.Received(1).Update(ledger);

        // Assert: Existing transaction should be deleted
        await _transactionRepository.Received(1).DeleteAsync(existingTx, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
