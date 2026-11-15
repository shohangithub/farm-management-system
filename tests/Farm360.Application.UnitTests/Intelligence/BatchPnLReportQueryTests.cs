using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Intelligence;

public class BatchPnLReportQueryTests
{
    [Fact]
    public async Task Includes_every_batch_animal_and_values_animals_on_hand()
    {
        var batchId = Guid.NewGuid();
        var valuation = new AnimalValuationService(new TestRulesProvider());
        var rate = valuation.LiveWeightRatePerKgBdt;

        var onHand = TestAnimals.Create(50_000m, 200m, "A");
        var onHandLedger = AnimalCostLedger.Create(TestAnimals.TenantId, onHand.Id, TestAnimals.FarmId, 50_000m);
        onHandLedger.UpdateFeedCost(10_000m);

        var sold = TestAnimals.Create(60_000m, 250m, "B");
        var soldLedger = AnimalCostLedger.Create(TestAnimals.TenantId, sold.Id, TestAnimals.FarmId, 60_000m);
        soldLedger.UpdateFeedCost(10_000m);
        sold.Sell(90_000m, new DateOnly(2025, 7, 1), Guid.NewGuid(), null, null);

        var noLedger = TestAnimals.Create(50_000m, 150m, "C");

        var animals = Substitute.For<IAnimalRepository>();
        animals.GetPagedAsync(1, Arg.Any<int>(), TestAnimals.FarmId, batchId,
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<AnimalSpecies?>(), Arg.Any<AnimalSex?>(), Arg.Any<AnimalStatus?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Animal>)new List<Animal> { onHand, sold, noLedger }, 3));

        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByBatchIdAsync(TestAnimals.FarmId, batchId, Arg.Any<CancellationToken>())
            .Returns(new List<AnimalCostLedger> { onHandLedger, soldLedger });

        var sale = FinancialTransaction.Create(TestAnimals.TenantId, TestAnimals.FarmId, TransactionType.Income,
            TransactionCategory.AnimalSale, 90_000m, new DateTime(2025, 7, 1), animalId: sold.Id);
        var transactions = Substitute.For<IFinancialTransactionRepository>();
        transactions.GetAllByFarmIdAsync(TestAnimals.FarmId, Arg.Any<CancellationToken>())
            .Returns(new List<FinancialTransaction> { sale });

        var sut = new GetBatchPnLReportQueryHandler(transactions, ledgers, animals, valuation);

        var r = await sut.Handle(new GetBatchPnLReportQuery(TestAnimals.FarmId, batchId), CancellationToken.None);

        const decimal totalCost = 60_000m + 70_000m + 50_000m;
        var unrealised = (200m * rate) + (150m * rate);

        r.TotalAnimals.Should().Be(3, "an animal without a ledger is still in the batch");
        r.OnHandAnimals.Should().Be(2);
        r.TotalIncomeBdt.Should().Be(90_000m);
        r.TotalCostBdt.Should().Be(totalCost);
        r.GrossProfitBdt.Should().Be(90_000m - totalCost);
        r.ReturnOnInvestmentPercent.Should().Be(Math.Round((90_000m - totalCost) / totalCost, 4), "ROI is a 0-1 fraction");
        r.UnrealisedValueBdt.Should().Be(unrealised);
        r.EstimatedTotalProfitBdt.Should().Be(90_000m + unrealised - totalCost);
        r.EstimatedRoiPercent.Should().Be(Math.Round((90_000m + unrealised - totalCost) / totalCost, 4));
    }
}
