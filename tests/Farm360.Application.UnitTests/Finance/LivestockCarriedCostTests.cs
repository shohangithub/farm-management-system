using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>
/// Growing costs (feed &amp; care) of animals still on the farm carried as part of their value:
/// the carried-cost service, the P&amp;L adjustment, the balance sheet and the farm-rule toggle.
/// </summary>
public class LivestockCarriedCostTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly BooksStart = new(2026, 6, 30);

    // C: on the farm before the books start (opening animal). A: bought and kept. B: bought, then sold 1 Sep.
    private static readonly Guid AnimalA = Guid.NewGuid();
    private static readonly Guid AnimalB = Guid.NewGuid();
    private static readonly Guid AnimalC = Guid.NewGuid();
    private static readonly DateOnly Bought = new(2026, 7, 1);
    private static readonly DateOnly SoldB = new(2026, 9, 1);

    private readonly InMemoryFinancialTransactionRepository _ledger = new();
    private readonly List<BalanceSheetEntry> _entries = [];
    private readonly FarmBusinessRules _rules = new();

    private static readonly LivestockCarriedCostData Registers = new(
        [
            new CarriedCostAnimal(AnimalA, Bought, null, false),
            new CarriedCostAnimal(AnimalB, Bought, SoldB, true),
            new CarriedCostAnimal(AnimalC, new DateOnly(2026, 5, 1), null, false),
        ],
        [
            new AnimalGrowingCost(AnimalC, new DateOnly(2026, 6, 1), 800m),    // before the books start
            new AnimalGrowingCost(AnimalA, new DateOnly(2026, 7, 15), 1_000m),
            new AnimalGrowingCost(AnimalB, new DateOnly(2026, 7, 15), 1_500m),
            new AnimalGrowingCost(AnimalA, new DateOnly(2026, 8, 15), 2_000m),
            new AnimalGrowingCost(AnimalB, new DateOnly(2026, 8, 15), 2_500m),
            new AnimalGrowingCost(AnimalB, new DateOnly(2026, 9, 10), 500m),   // after B left: never carried
            new AnimalGrowingCost(AnimalA, new DateOnly(2026, 10, 5), 700m),   // after the dates asked about
        ]);

    // ── The carried-cost service ────────────────────────────────────────────

    [Fact]
    public async Task CarriedCost_CountsAnimalsOnTheFarm_NotSoldOnes_NorCostsAfterTheDate()
    {
        var service = Service();

        (await service.GetCarriedCostAsync(FarmId, new DateOnly(2026, 8, 31))).Should().Be(7_800m,
            "A 3,000 + B 4,000 + C 800: everything on the farm on 31 Aug");
        (await service.GetCarriedCostAsync(FarmId, new DateOnly(2026, 9, 30))).Should().Be(3_800m,
            "B was sold on 1 Sep, so its 4,000 has left; its 500 after the sale was never carried; A's 700 on 5 Oct is after the date");
        (await service.GetCarriedCostAsync(FarmId, SoldB.AddDays(-1))).Should().Be(7_800m, "still on the farm the day before the sale");
        (await service.GetCarriedCostAsync(FarmId, SoldB)).Should().Be(3_800m, "not on the farm on its exit day");
        (await service.GetCarriedCostAsync(FarmId, new DateOnly(2026, 4, 30))).Should().Be(0m, "before any animal arrived");
    }

    [Fact]
    public void CarriedCost_NeverIncludesThePurchasePrice_AndCostsBeforeArrivalCountFromArrival()
    {
        var animal = Guid.NewGuid();
        var data = new LivestockCarriedCostData(
            [new CarriedCostAnimal(animal, new DateOnly(2026, 7, 10), null, false)],
            [new AnimalGrowingCost(animal, new DateOnly(2026, 7, 1), 300m)]);

        var movements = LivestockCarriedCostService.BuildMovements(data, new DateOnly(2026, 12, 31));

        movements.Should().ContainSingle().Which.Should().Be(new CarriedCostMovement(new DateOnly(2026, 7, 10), 300m),
            "only growing costs are inputs (the purchase price stays in Livestock 1300), carried from the day the animal arrived");
    }

    [Fact]
    public void CarriedCost_AnimalThatLeftWithNoExitDate_IsNeverCarried()
    {
        var animal = Guid.NewGuid();
        var data = new LivestockCarriedCostData(
            [new CarriedCostAnimal(animal, new DateOnly(2026, 7, 1), null, HasLeftFarm: true)],
            [new AnimalGrowingCost(animal, new DateOnly(2026, 7, 5), 300m)]);

        LivestockCarriedCostService.BuildMovements(data, new DateOnly(2026, 12, 31)).Should().BeEmpty();
    }

    // ── Profit & loss ───────────────────────────────────────────────────────

    [Fact]
    public async Task IncomeStatement_PeriodWithASale_CarriesForwardTheKeptAnimal_AndReleasesTheSoldOnesCost()
    {
        await PostTheBooks();

        var statement = await IncomeStatement(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30));

        // Ledger: sale 50,000 − cost of B 40,000 − feed 5,000 = 5,000. Carried 31 Jul 3,300 → 30 Sep 3,800.
        var line = statement.Sections.Single(s => s.Key == "cost-of-sales").Lines
            .Single(l => l.AccountCode == AccountChart.GrowingCostsCarriedAccount);
        line.AmountBdt.Should().Be(-500m, "carried(30 Sep) − carried(31 Jul) = 500 taken off the costs");
        line.AccountName.Should().Be("Feed & care of animals still on the farm (carried to next period)");
        statement.Totals.NetProfitBdt.Should().Be(5_500m,
            "B: 50,000 − 40,000 − its 4,000 feed & care = 6,000, less 500 fed after the sale; A's August feed is carried");
    }

    [Fact]
    public async Task MonthlyPnL_TwelveMonths_AddUpToTheYear_AndTheBalanceSheetProfit()
    {
        await PostTheBooks();

        var months = 0m;
        for (var m = 7; m <= 9; m++)
        {
            var report = await new GetMonthlyPnLReportQueryHandler(Engine())
                .Handle(new GetMonthlyPnLReportQuery(FarmId, 2026, m), CancellationToken.None);
            months += report.NetProfitBdt;
        }

        var sheet = await BalanceSheet(new DateOnly(2026, 9, 30));
        months.Should().Be(sheet.CurrentYearProfitBdt);
    }

    // ── Balance sheet ───────────────────────────────────────────────────────

    [Fact]
    public async Task BalanceSheet_ShowsPurchaseCostAndFeedAndCare_AndBalancesToThePaisa()
    {
        await PostTheBooks();

        var sheet = await BalanceSheet(new DateOnly(2026, 9, 30));

        Line(sheet.Assets, AccountChart.LivestockAccount).Should().Be(50_000m, "A 30,000 + C 20,000 at purchase cost");
        Line(sheet.Assets, AccountChart.LivestockCarriedCostAccount).Should().Be(3_800m);
        sheet.Assets.Lines.Single(l => l.LineCode == AccountChart.LivestockAccount).LineName.Should().Be("Animals on the farm: purchase cost");
        sheet.Assets.Lines.Single(l => l.LineCode == AccountChart.LivestockCarriedCostAccount).LineName.Should().Be("Animals on the farm: feed & care to date");
        Line(sheet.Equity, AccountChart.RetainedEarningsAccount).Should().Be(800m, "what was carried at the books start opens in equity");
        Line(sheet.Equity, AccountChart.CurrentYearProfitLine).Should().Be(5_500m, "ledger 2,500 + carried 3,800 − 800 at the start");
        sheet.TotalAssetsBdt.Should().Be(126_300m);
        (sheet.TotalAssetsBdt - sheet.TotalLiabilitiesAndEquityBdt).Should().Be(0m);
        sheet.DifferenceBdt.Should().Be(0m);
        sheet.IsBalanced.Should().BeTrue();

        // Every date balances, including the books start and the comparative.
        foreach (var day in new[] { BooksStart, new DateOnly(2026, 7, 31), new DateOnly(2026, 8, 31), SoldB, new DateOnly(2027, 6, 30), new DateOnly(2027, 8, 31) })
        {
            var s = await BalanceSheet(day);
            s.DifferenceBdt.Should().Be(0m, $"on {day}");
        }
    }

    [Fact]
    public async Task TrialBalance_StaysPureLedger_AndBalances()
    {
        await PostTheBooks();

        var trial = await new GetTrialBalanceQueryHandler(Engine())
            .Handle(new GetTrialBalanceQuery(FarmId, new DateOnly(2026, 9, 30).ToDateTime(TimeOnly.MinValue)), CancellationToken.None);

        trial.Lines.Should().NotContain(l => l.AccountCode == AccountChart.LivestockCarriedCostAccount || l.AccountCode == AccountChart.GrowingCostsCarriedAccount);
        trial.IsBalanced.Should().BeTrue();
    }

    // ── The farm rule ───────────────────────────────────────────────────────

    [Fact]
    public async Task RuleOff_GivesTodaysLedgerNumbers()
    {
        await PostTheBooks();
        _rules.CarryLivestockGrowingCosts = false;

        var withRuleOff = await IncomeStatement(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30));
        var withoutService = await IncomeStatement(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30), carried: false);
        withRuleOff.Should().BeEquivalentTo(withoutService);
        withRuleOff.Totals.NetProfitBdt.Should().Be(5_000m);

        var sheetOff = await BalanceSheet(new DateOnly(2026, 9, 30));
        var sheetWithout = await BalanceSheet(new DateOnly(2026, 9, 30), carried: false);
        sheetOff.Should().BeEquivalentTo(sheetWithout);
        sheetOff.Assets.Lines.Should().NotContain(l => l.LineCode == AccountChart.LivestockCarriedCostAccount);
        Line(sheetOff.Equity, AccountChart.CurrentYearProfitLine).Should().Be(2_500m);

        (await Service().GetPeriodAdjustmentAsync(FarmId, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30))).Should().Be(0m);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>The ledger side of the story: what was bought, fed and sold.</summary>
    private async Task PostTheBooks()
    {
        Opening(AccountChart.CashAccount, 100_000m);
        Opening(AccountChart.LivestockAccount, 20_000m); // animal C
        Opening(AccountChart.OwnerCapitalAccount, 120_000m);

        await Post(TransactionCategory.AnimalPurchase, 30_000m, Bought);
        await Post(TransactionCategory.AnimalPurchase, 40_000m, Bought);
        await Post(TransactionCategory.FeedCost, 2_500m, new DateOnly(2026, 7, 15));
        await Post(TransactionCategory.FeedCost, 4_500m, new DateOnly(2026, 8, 15));
        await Post(TransactionCategory.AnimalSale, 50_000m, SoldB);
        await Post(TransactionCategory.CostOfAnimalsSold, 40_000m, SoldB, SettlementAccount.Livestock);
        await Post(TransactionCategory.FeedCost, 500m, new DateOnly(2026, 9, 10));
        await Post(TransactionCategory.FeedCost, 700m, new DateOnly(2026, 10, 5));
    }

    private LivestockCarriedCostService Service()
    {
        var source = Substitute.For<ILivestockCarriedCostSource>();
        source.LoadAsync(FarmId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Registers);
        var rules = Substitute.For<IFarmBusinessRulesProvider>();
        rules.Current.Returns(_ => _rules);
        return new LivestockCarriedCostService(source, rules);
    }

    private FinancialStatementEngine Engine(bool carried = true)
    {
        var settings = Substitute.For<IFarmAccountingSettingsRepository>();
        settings.GetByFarmIdAsync(FarmId, Arg.Any<CancellationToken>())
            .Returns(FarmAccountingSettings.Create(TenantId, FarmId, 7, BooksStart));
        var entries = Substitute.For<IBalanceSheetEntryRepository>();
        entries.GetByFarmIdAsync(FarmId, Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<BalanceSheetEntry>)_entries.ToList());
        var assets = Substitute.For<IFixedAssetRepository>();
        assets.GetByFarmIdAsync(FarmId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<FixedAsset>)[]);
        return new FinancialStatementEngine(_ledger, settings, entries, assets, carried ? Service() : null);
    }

    private void Opening(string account, decimal amount) =>
        _entries.Add(BalanceSheetEntry.Create(TenantId, FarmId, $"OB-{_entries.Count + 1:0000}", BalanceSheetEntryType.Opening,
            BooksStart, account, amount, "Opening"));

    private async Task Post(TransactionCategory category, decimal amount, DateOnly date,
        SettlementAccount settlement = SettlementAccount.Cash) =>
        await _ledger.AddAsync(FinancialTransaction.Create(TenantId, FarmId, AccountChart.For(category).Type, category, amount,
            date.ToDateTime(TimeOnly.MinValue), settlement: settlement));

    private async Task<IncomeStatementDto> IncomeStatement(DateOnly from, DateOnly to, bool carried = true) =>
        await new GetIncomeStatementQueryHandler(Engine(carried))
            .Handle(new GetIncomeStatementQuery(FarmId, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue)), CancellationToken.None);

    private async Task<BalanceSheetDto> BalanceSheet(DateOnly asOf, bool carried = true)
    {
        var reconciliation = Substitute.For<IAccountingReconciliationService>();
        reconciliation.ReconcileBalancesAsync(FarmId, Arg.Any<IReadOnlyDictionary<string, decimal>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReconciliationLineDto>());
        return await new GetBalanceSheetQueryHandler(Engine(carried), reconciliation)
            .Handle(new GetBalanceSheetQuery(FarmId, asOf.ToDateTime(TimeOnly.MinValue)), CancellationToken.None);
    }

    private static decimal Line(BalanceSheetSectionDto section, string code) =>
        section.Lines.Single(l => l.LineCode == code).AmountBdt;
}
