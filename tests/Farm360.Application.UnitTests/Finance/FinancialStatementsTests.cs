using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>Balance sheet, income statement, trial balance and fixed-asset schedule on hand-computable books.</summary>
public class FinancialStatementsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    // July–June financial year; books start at the 2025-26 year end.
    private static readonly DateOnly BooksStart = new(2026, 6, 30);
    private static readonly DateOnly YearEnd = new(2027, 6, 30);

    private readonly InMemoryFinancialTransactionRepository _ledger = new();
    private readonly List<BalanceSheetEntry> _entries = [];
    private readonly List<FixedAsset> _assets = [];
    private FarmAccountingSettings? _settings = FarmAccountingSettings.Create(TenantId, FarmId, 7, BooksStart);

    // ── Depreciation ────────────────────────────────────────────────────────

    [Fact]
    public void ReducingBalance_ChargesTheRateOnEachYearsOpeningValue()
    {
        var basis = new DepreciationBasis(new DateOnly(2025, 7, 1), 100_000m, DepreciationMethod.ReducingBalance, 10m, 0m, 0m);

        DepreciationCalculator.ValueAtEndOf(basis, new DateOnly(2026, 6, 30), 7).Should().Be(90_000m);
        DepreciationCalculator.ValueAtEndOf(basis, new DateOnly(2027, 6, 30), 7).Should().Be(81_000m);
    }

    [Fact]
    public void ReducingBalance_PartYear_IsChargedByDays()
    {
        var basis = new DepreciationBasis(new DateOnly(2026, 1, 1), 100_000m, DepreciationMethod.ReducingBalance, 10m, 0m, 0m);

        // 1 Jan – 30 Jun 2026 is 181 of the 365 days in FY 2025-26.
        DepreciationCalculator.ValueAtEndOf(basis, new DateOnly(2026, 6, 30), 7).Should().Be(95_041.10m);
    }

    [Fact]
    public void StraightLine_StopsAtTheResidualValue()
    {
        var basis = new DepreciationBasis(new DateOnly(2025, 7, 1), 50_000m, DepreciationMethod.StraightLine, 0m, 5m, 5_000m);

        DepreciationCalculator.ValueAtEndOf(basis, new DateOnly(2026, 6, 30), 7).Should().Be(41_000m);
        DepreciationCalculator.ValueAtEndOf(basis, new DateOnly(2035, 6, 30), 7).Should().Be(5_000m);
    }

    // ── Balance sheet ───────────────────────────────────────────────────────

    [Fact]
    public async Task BalanceSheet_WithOpeningPositionAndDepreciation_Balances_AndSplitsRetainedEarnings()
    {
        Opening(AccountChart.CashAccount, 50_000m);
        Opening(AccountChart.OwnerCapitalAccount, 120_000m);
        _assets.Add(FixedAsset.Create(TenantId, FarmId, "FA-0001", "Cattle shed", FixedAssetCategory.BuildingAndShed,
            FixedAssetAcquisition.Opening, BooksStart, 70_000m, DepreciationMethod.ReducingBalance, 10m, 0m, 0m));
        await Post(TransactionCategory.MilkSale, 10_000m, new DateOnly(2026, 8, 1));
        await Post(TransactionCategory.LaborCost, 2_000m, new DateOnly(2026, 9, 1));

        var sheet = await BalanceSheet(YearEnd);

        Line(sheet.Assets, AccountChart.FixedAssetAccount).Should().Be(70_000m);
        Line(sheet.Assets, AccountChart.AccumulatedDepreciationAccount).Should().Be(-7_000m, "10% of the opening 70,000");
        Line(sheet.Assets, AccountChart.CashAccount).Should().Be(58_000m);
        Line(sheet.Equity, AccountChart.CurrentYearProfitLine).Should().Be(1_000m, "10,000 − 2,000 − 7,000 depreciation");
        Line(sheet.Equity, AccountChart.RetainedEarningsAccount).Should().Be(0m);
        sheet.TotalAssetsBdt.Should().Be(121_000m);
        sheet.IsBalanced.Should().BeTrue();
        sheet.IsConfigured.Should().BeTrue();

        // Comparative: the previous year end is the books start — the opening position.
        sheet.PriorDate.Should().Be(BooksStart.ToDateTime(TimeOnly.MinValue));
        sheet.PriorTotalAssetsBdt.Should().Be(120_000m);
        sheet.PriorDifferenceBdt.Should().Be(0m);

        // Next year the profit is brought forward.
        var next = await BalanceSheet(new DateOnly(2027, 12, 31));
        Line(next.Equity, AccountChart.RetainedEarningsAccount).Should().Be(1_000m);
        next.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task BalanceSheet_IncompleteOpeningPosition_ShowsTheDifference_InsteadOfPluggingIt()
    {
        Opening(AccountChart.CashAccount, 50_000m); // the capital behind it was never entered

        var sheet = await BalanceSheet(YearEnd);

        sheet.IsBalanced.Should().BeFalse();
        sheet.DifferenceBdt.Should().Be(50_000m);
        sheet.Message.Should().Contain("opening balances");

        var trial = await new GetTrialBalanceQueryHandler(Engine())
            .Handle(new GetTrialBalanceQuery(FarmId, YearEnd.ToDateTime(TimeOnly.MinValue)), CancellationToken.None);
        trial.DifferenceBdt.Should().Be(50_000m, "the trial balance is out by the same amount");
    }

    [Fact]
    public async Task BalanceSheet_PostingsOnOrBeforeTheBooksStart_AreReplacedByTheOpeningPosition()
    {
        await Post(TransactionCategory.OwnerCapital, 999_000m, BooksStart); // already in the opening balances
        Opening(AccountChart.CashAccount, 40_000m);
        Opening(AccountChart.OwnerCapitalAccount, 40_000m);

        var sheet = await BalanceSheet(YearEnd);

        Line(sheet.Assets, AccountChart.CashAccount).Should().Be(40_000m);
        sheet.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task Adjustment_AccruesAtTheYearEnd_AndReversesInTheNextYear()
    {
        Opening(AccountChart.CashAccount, 20_000m);
        Opening(AccountChart.OwnerCapitalAccount, 20_000m);
        _entries.Add(BalanceSheetEntry.Create(TenantId, FarmId, "ADJ-0001", BalanceSheetEntryType.Adjustment, YearEnd,
            AccountChart.ProvisionsAccount, 3_000m, "June electricity bill not yet received",
            profitAccountCode: "5050", reversalDate: YearEnd.AddDays(1)));

        var atYearEnd = await BalanceSheet(YearEnd);
        Line(atYearEnd.Liabilities, AccountChart.ProvisionsAccount).Should().Be(3_000m);
        Line(atYearEnd.Equity, AccountChart.CurrentYearProfitLine).Should().Be(-3_000m);
        atYearEnd.IsBalanced.Should().BeTrue();

        var afterReversal = await BalanceSheet(new DateOnly(2027, 7, 31));
        afterReversal.Liabilities.Lines.Should().NotContain(l => l.LineCode == AccountChart.ProvisionsAccount && l.AmountBdt != 0m);
        Line(afterReversal.Equity, AccountChart.RetainedEarningsAccount).Should().Be(-3_000m);
        Line(afterReversal.Equity, AccountChart.CurrentYearProfitLine).Should().Be(3_000m, "the reversal credits the expense; the real bill, when posted, will net against it");
        afterReversal.IsBalanced.Should().BeTrue();
    }

    // ── Fixed assets: purchase, disposal, income statement ─────────────────

    [Fact]
    public async Task FixedAssetDisposal_GainNetsAgainstDepreciation_AndStatementsAgree()
    {
        var bought = new DateOnly(2026, 7, 1);
        var sold = new DateOnly(2027, 6, 30);
        Opening(AccountChart.CashAccount, 100_000m);
        Opening(AccountChart.OwnerCapitalAccount, 100_000m);

        var tractor = FixedAsset.Create(TenantId, FarmId, "FA-0001", "Tractor", FixedAssetCategory.Vehicle,
            FixedAssetAcquisition.Cash, bought, 100_000m, DepreciationMethod.ReducingBalance, 10m, 0m, 0m);
        tractor.RecordDisposal(sold, 95_000m);
        _assets.Add(tractor);
        await Post(TransactionCategory.FixedAssetPurchase, 100_000m, bought);
        await Post(TransactionCategory.AssetDisposal, 95_000m, sold);

        var sheet = await BalanceSheet(YearEnd);
        sheet.Assets.Lines.Where(l => l.LineCode is AccountChart.FixedAssetAccount or AccountChart.AccumulatedDepreciationAccount)
            .Sum(l => l.AmountBdt).Should().Be(0m, "the tractor is gone");
        Line(sheet.Assets, AccountChart.CashAccount).Should().Be(95_000m);
        Line(sheet.Equity, AccountChart.CurrentYearProfitLine).Should().Be(-5_000m, "10,000 depreciation less a 5,000 gain on sale");
        sheet.IsBalanced.Should().BeTrue();

        var income = await new GetIncomeStatementQueryHandler(Engine())
            .Handle(new GetIncomeStatementQuery(FarmId, null, YearEnd.ToDateTime(TimeOnly.MinValue)), CancellationToken.None);
        income.FromDate.Should().Be(new DateTime(2026, 7, 1));
        var operating = income.Sections.Single(s => s.Key == "operating-expenses");
        operating.Lines.Single(l => l.AccountCode == AccountChart.DepreciationAccount).AmountBdt.Should().Be(10_000m);
        operating.Lines.Single(l => l.AccountCode == AccountChart.AssetDisposalAccount).AmountBdt.Should().Be(-5_000m);
        income.Totals.NetProfitBdt.Should().Be(sheet.CurrentYearProfitBdt, "both statements read the same lines");

        var schedule = FixedAssetSchedule.Line(tractor, new DateOnly(2026, 7, 1), YearEnd, 7)!;
        schedule.AdditionsBdt.Should().Be(100_000m);
        schedule.DisposalsBdt.Should().Be(100_000m);
        schedule.DepreciationChargeBdt.Should().Be(10_000m);
        schedule.DepreciationOnDisposalBdt.Should().Be(10_000m);
        schedule.WrittenDownValueBdt.Should().Be(0m);
    }

    [Fact]
    public async Task IncomeStatement_SplitsCostOfSalesFromOverheads()
    {
        Opening(AccountChart.CashAccount, 10_000m);
        Opening(AccountChart.OwnerCapitalAccount, 10_000m);
        await Post(TransactionCategory.AnimalSale, 90_000m, new DateOnly(2026, 10, 1));
        await Post(TransactionCategory.CostOfAnimalsSold, 60_000m, new DateOnly(2026, 10, 1), SettlementAccount.Livestock);
        await Post(TransactionCategory.FeedCost, 12_000m, new DateOnly(2026, 10, 1));
        await Post(TransactionCategory.Utilities, 1_000m, new DateOnly(2026, 10, 1));
        await Post(TransactionCategory.InterestExpense, 500m, new DateOnly(2026, 10, 1));

        var income = await new GetIncomeStatementQueryHandler(Engine())
            .Handle(new GetIncomeStatementQuery(FarmId, null, YearEnd.ToDateTime(TimeOnly.MinValue)), CancellationToken.None);

        income.Totals.RevenueBdt.Should().Be(90_000m);
        income.Totals.CostOfSalesBdt.Should().Be(72_000m);
        income.Totals.GrossProfitBdt.Should().Be(18_000m);
        income.Totals.OperatingExpensesBdt.Should().Be(1_000m);
        income.Totals.FinanceCostsBdt.Should().Be(500m);
        income.Totals.NetProfitBdt.Should().Be(16_500m);
        income.PriorTotals!.NetProfitBdt.Should().Be(0m);
    }

    [Fact]
    public async Task WithoutSettings_TheStatementsRunFromTheFirstPosting()
    {
        _settings = null;
        await Post(TransactionCategory.OwnerCapital, 5_000m, new DateOnly(2024, 1, 1));
        await Post(TransactionCategory.MilkSale, 1_000m, new DateOnly(2025, 1, 1));
        await Post(TransactionCategory.MilkSale, 2_000m, new DateOnly(2026, 8, 1));

        var sheet = await BalanceSheet(YearEnd);

        sheet.IsConfigured.Should().BeFalse();
        sheet.Message.Should().Contain("Accounts setup");
        Line(sheet.Equity, AccountChart.RetainedEarningsAccount).Should().Be(1_000m);
        Line(sheet.Equity, AccountChart.CurrentYearProfitLine).Should().Be(2_000m);
        sheet.IsBalanced.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void Opening(string account, decimal amount) =>
        _entries.Add(BalanceSheetEntry.Create(TenantId, FarmId, $"OB-{_entries.Count + 1:0000}", BalanceSheetEntryType.Opening,
            BooksStart, account, amount, "Opening"));

    private async Task Post(TransactionCategory category, decimal amount, DateOnly date,
        SettlementAccount settlement = SettlementAccount.Cash) =>
        await _ledger.AddAsync(FinancialTransaction.Create(TenantId, FarmId, AccountChart.For(category).Type, category, amount,
            date.ToDateTime(TimeOnly.MinValue), settlement: settlement));

    private FinancialStatementEngine Engine()
    {
        var settings = Substitute.For<IFarmAccountingSettingsRepository>();
        settings.GetByFarmIdAsync(FarmId, Arg.Any<CancellationToken>()).Returns(_ => _settings);
        var entries = Substitute.For<IBalanceSheetEntryRepository>();
        entries.GetByFarmIdAsync(FarmId, Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<BalanceSheetEntry>)_entries.ToList());
        var assets = Substitute.For<IFixedAssetRepository>();
        assets.GetByFarmIdAsync(FarmId, Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<FixedAsset>)_assets.ToList());
        return new FinancialStatementEngine(_ledger, settings, entries, assets);
    }

    private async Task<BalanceSheetDto> BalanceSheet(DateOnly asOf)
    {
        var reconciliation = Substitute.For<IAccountingReconciliationService>();
        reconciliation.ReconcileAsync(FarmId, Arg.Any<IReadOnlyCollection<FinancialTransaction>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReconciliationLineDto>());
        return await new GetBalanceSheetQueryHandler(Engine(), reconciliation)
            .Handle(new GetBalanceSheetQuery(FarmId, asOf.ToDateTime(TimeOnly.MinValue)), CancellationToken.None);
    }

    private static decimal Line(BalanceSheetSectionDto section, string code) =>
        section.Lines.Single(l => l.LineCode == code).AmountBdt;
}
