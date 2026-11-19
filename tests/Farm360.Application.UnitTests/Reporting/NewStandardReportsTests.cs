using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Farm360.Application.Analytics.Services;
using Farm360.Application.Finance.Services;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Application.Reporting.Definitions.Health;
using Farm360.Application.Reporting.Definitions.Livestock;
using Farm360.Application.Reporting.Model;
using Farm360.Application.UnitTests.Finance;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class TradeAgeingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly AsAt = new(2026, 11, 30);

    [Theory]
    [InlineData(-5, AgeingBucket.Current)]
    [InlineData(0, AgeingBucket.Current)]
    [InlineData(1, AgeingBucket.Days1To30)]
    [InlineData(30, AgeingBucket.Days1To30)]
    [InlineData(31, AgeingBucket.Days31To60)]
    [InlineData(60, AgeingBucket.Days31To60)]
    [InlineData(61, AgeingBucket.Days61To90)]
    [InlineData(90, AgeingBucket.Days61To90)]
    [InlineData(91, AgeingBucket.Over90)]
    public void Bucket_boundaries(int daysOverdue, AgeingBucket expected) =>
        TradeAgeing.Bucket(daysOverdue).Should().Be(expected);

    [Fact]
    public void Due_today_is_current_and_one_day_later_is_overdue()
    {
        TradeAgeing.Bucket(TradeAgeing.DaysOverdue(AsAt.AddDays(-10), AsAt, AsAt)).Should().Be(AgeingBucket.Current);
        TradeAgeing.Bucket(TradeAgeing.DaysOverdue(AsAt.AddDays(-10), AsAt.AddDays(-1), AsAt)).Should().Be(AgeingBucket.Days1To30);
    }

    [Fact]
    public void A_bill_without_a_due_date_ages_from_its_bill_date() =>
        TradeAgeing.DaysOverdue(AsAt.AddDays(-45), null, AsAt).Should().Be(45);

    [Fact]
    public void Outstanding_as_at_ignores_payments_made_after_the_date_and_bills_raised_after_it()
    {
        var bill = Bill("Rahim Traders", 10_000m, AsAt.AddDays(-40), AsAt.AddDays(-35));
        var early = TradeBillPayment.Create(TenantId, bill.Id, FarmId, AsAt.AddDays(-20), 3_000m, "Cash", null, null);
        var late = TradeBillPayment.Create(TenantId, bill.Id, FarmId, AsAt.AddDays(2), 7_000m, "Cash", null, null);

        TradeAgeing.OutstandingAsAt(bill, [early, late], AsAt).Should().Be(7_000m);
        TradeAgeing.OutstandingAsAt(Bill("X", 500m, AsAt.AddDays(1), null), [], AsAt).Should().Be(0m);
    }

    [Fact]
    public void Section_puts_each_partys_bills_in_their_buckets()
    {
        var bills = new[]
        {
            Bill("Karim Feeds", 1_000m, AsAt.AddDays(-5), AsAt.AddDays(10)),   // current
            Bill("Karim Feeds", 2_000m, AsAt.AddDays(-70), AsAt.AddDays(-61)), // 61 days
            Bill("karim feeds ", 4_000m, AsAt.AddDays(-200), null),            // 200 days, same party
            Bill("Hasan Agro", 500m, AsAt.AddDays(-31), AsAt.AddDays(-31)),    // 31 days
        };

        var rows = TradeAgeingReportDefinition.BuildSection(bills, Array.Empty<TradeBillPayment>().ToLookup(p => p.BillId), AsAt, "A");

        rows.Should().HaveCount(2);
        var karim = rows.Single(r => r.Party.StartsWith("Karim", StringComparison.OrdinalIgnoreCase));
        karim.Bills.Should().Be(3);
        karim.CurrentBdt.Should().Be(1_000m);
        karim.Days61To90Bdt.Should().Be(2_000m);
        karim.Over90Bdt.Should().Be(4_000m);
        karim.TotalBdt.Should().Be(7_000m);
        karim.MaxDaysOverdue.Should().Be(200);
        rows.Single(r => r.Party == "Hasan Agro").Days31To60Bdt.Should().Be(500m);
    }

    private static TradeBill Bill(string party, decimal amount, DateOnly billDate, DateOnly? due) =>
        TradeBill.Create(TenantId, FarmId, TradeBillKind.Payable, party, Guid.NewGuid(), TransactionCategory.InventoryPurchase,
            amount, billDate, due, "REF", "test");
}

public class CashFlowStatementTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    [Fact]
    public async Task Opening_and_closing_cash_equal_the_balance_sheet_cash_and_flows_explain_the_change()
    {
        var repo = new InMemoryFinancialTransactionRepository();
        void Post(TransactionType type, TransactionCategory category, decimal amount, DateOnly date, SettlementAccount settlement = SettlementAccount.Cash) =>
            repo.Rows.Add(FinancialTransaction.Create(TenantId, FarmId, type, category, amount, date.ToDateTime(TimeOnly.MinValue), settlement: settlement));

        Post(TransactionType.Income, TransactionCategory.OwnerCapital, 100_000m, new DateOnly(2026, 1, 5));
        Post(TransactionType.Expense, TransactionCategory.AnimalPurchase, 40_000m, new DateOnly(2026, 1, 10));
        Post(TransactionType.Expense, TransactionCategory.InventoryPurchase, 10_000m, new DateOnly(2026, 1, 12), SettlementAccount.Payable);
        Post(TransactionType.Expense, TransactionCategory.SupplierPayment, 4_000m, new DateOnly(2026, 2, 2));
        Post(TransactionType.Income, TransactionCategory.AnimalSale, 60_000m, new DateOnly(2026, 2, 10));
        Post(TransactionType.Expense, TransactionCategory.CostOfAnimalsSold, 40_000m, new DateOnly(2026, 2, 10), SettlementAccount.Livestock);
        Post(TransactionType.Expense, TransactionCategory.FixedAssetPurchase, 20_000m, new DateOnly(2026, 2, 15));
        Post(TransactionType.Income, TransactionCategory.LoanDisbursement, 30_000m, new DateOnly(2026, 2, 20));
        Post(TransactionType.Expense, TransactionCategory.LaborCost, 5_000m, new DateOnly(2026, 3, 1));

        var from = new DateOnly(2026, 2, 1);
        var to = new DateOnly(2026, 2, 28);
        var ledger = await new FinancialStatementEngine(repo).LoadAsync(FarmId, to);

        var statement = CashFlowStatement.Build(ledger, from, to);

        statement.OpeningCashBdt.Should().Be(ledger.BalancesAt(new DateOnly(2026, 1, 31))[AccountChart.CashAccount]).And.Be(60_000m);
        statement.ClosingCashBdt.Should().Be(ledger.BalancesAt(to)[AccountChart.CashAccount]).And.Be(126_000m);
        statement.NetChangeBdt.Should().Be(66_000m);
        statement.UnexplainedBdt.Should().Be(0m);
        statement.NetFor(CashFlowActivity.Operating).Should().Be(56_000m); // sale 60,000 − supplier payment 4,000
        statement.NetFor(CashFlowActivity.Investing).Should().Be(-20_000m);
        statement.NetFor(CashFlowActivity.Financing).Should().Be(30_000m);
        statement.Lines.Should().NotContain(l => l.Code == AccountChart.For(TransactionCategory.CostOfAnimalsSold).Code, "non-cash postings never reach the cash flow");

        var rows = CashFlowStatementReportDefinition.ToRows(statement, ReportLanguage.En);
        rows.Where(r => r.FlowBdt is not null).Sum(r => r.FlowBdt!.Value).Should().Be(statement.NetChangeBdt);
    }

    [Fact]
    public void Classification_follows_the_chart_groups()
    {
        CashFlowStatement.Classify(TransactionCategory.AnimalPurchase).Should().Be(CashFlowActivity.Operating);
        CashFlowStatement.Classify(TransactionCategory.CustomerReceipt).Should().Be(CashFlowActivity.Operating);
        CashFlowStatement.Classify(TransactionCategory.InterestExpense).Should().Be(CashFlowActivity.Operating);
        CashFlowStatement.Classify(TransactionCategory.AssetDisposal).Should().Be(CashFlowActivity.Investing);
        CashFlowStatement.Classify(TransactionCategory.LoanRepayment).Should().Be(CashFlowActivity.Financing);
        CashFlowStatement.Classify(TransactionCategory.OwnerDrawing).Should().Be(CashFlowActivity.Financing);
    }
}

public class BatchPerformanceTests
{
    private static readonly DateOnly From = new(2026, 9, 1);
    private static readonly DateOnly To = new(2026, 9, 30);

    [Fact]
    public void Fcr_is_feed_between_the_weigh_ins_over_the_gain_and_unweighed_animals_are_flagged()
    {
        var weighed = Guid.NewGuid();
        var notWeighed = Guid.NewGuid();
        var animals = new Dictionary<Guid, HerdMetrics.Residency>
        {
            [weighed] = new(new DateOnly(2026, 1, 1), null),
            [notWeighed] = new(new DateOnly(2026, 1, 1), null),
        };

        var weighIns = new[]
        {
            new HerdMetrics.WeighIn(weighed, new DateOnly(2026, 9, 1), 200m),
            new HerdMetrics.WeighIn(weighed, new DateOnly(2026, 9, 11), 210m),
            new HerdMetrics.WeighIn(notWeighed, new DateOnly(2026, 9, 5), 300m), // one weigh-in only
        };

        var feed = Enumerable.Range(1, 15).Select(d => new AnimalFeedDay(weighed, new DateOnly(2026, 9, d), 5m, 50m))
            .Concat(Enumerable.Range(1, 15).Select(d => new AnimalFeedDay(notWeighed, new DateOnly(2026, 9, d), 4m, 40m)))
            .ToList();

        var m = BatchPerformanceCalculator.Compute(animals, weighIns, feed, From, To);

        m.WeightGainKg.Should().Be(10m);
        m.AnimalsWeighed.Should().Be(1);
        m.AnimalsMissingWeights.Should().Be(1);
        m.Fcr.Should().Be(5.00m, "50 kg eaten on 1–10 Sep (5 kg × 10 days) for 10 kg gained");
        m.FeedCostPerKgGainBdt.Should().Be(50.00m);
        m.FeedKg.Should().Be(135m, "the feed column is every animal's confirmed feed in the period");
        m.FeedCostBdt.Should().Be(1_350m);
        m.AdgKgPerDay.Should().Be(1.0m);
        m.HeadStart.Should().Be(2);
        m.FeedCostPerHeadDayBdt.Should().Be(decimal.Round(1_350m / 60m, 2));

        BatchPerformanceReportDefinition.ToRow("B1", m, bn: false).Flags.Should().Contain("1 of 2 not weighed twice");
    }

    [Fact]
    public void Without_two_weigh_ins_there_is_no_gain_and_no_fcr_rather_than_an_invented_one()
    {
        var id = Guid.NewGuid();
        var animals = new Dictionary<Guid, HerdMetrics.Residency> { [id] = new(new DateOnly(2026, 1, 1), null) };
        var feed = new[] { new AnimalFeedDay(id, new DateOnly(2026, 9, 3), 6m, 60m) };

        var m = BatchPerformanceCalculator.Compute(animals, [new HerdMetrics.WeighIn(id, new DateOnly(2026, 9, 2), 250m)], feed, From, To);

        m.WeightGainKg.Should().BeNull();
        m.Fcr.Should().BeNull();
        m.FeedCostPerKgGainBdt.Should().BeNull();
        m.AdgKgPerDay.Should().BeNull();
        m.FeedKg.Should().Be(6m);
    }

    [Fact]
    public void Head_counts_follow_the_exit_date_rule()
    {
        var animals = new Dictionary<Guid, HerdMetrics.Residency>
        {
            [Guid.NewGuid()] = new(new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 16)), // left mid-period
            [Guid.NewGuid()] = new(new DateOnly(2026, 9, 10), null),                     // arrived mid-period
            [Guid.NewGuid()] = new(new DateOnly(2026, 1, 1), null),
        };

        var m = BatchPerformanceCalculator.Compute(animals, [], [], From, To);

        m.HeadStart.Should().Be(2);
        m.HeadEnd.Should().Be(2);
        m.HeadDays.Should().Be(15 + 21 + 30);
    }
}

public class MortalityRateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 9, 1);
    private static readonly DateOnly To = new(2026, 9, 30);

    [Fact]
    public void Rate_is_deaths_over_the_average_on_farm_head_count()
    {
        var herd = Enumerable.Range(1, 10).Select(i => NewAnimal($"M-{i}")).ToList();
        herd[0].RecordDeath(DisposalReason.Disease, new DateOnly(2026, 9, 16), null);

        var avg = HerdMetrics.AverageHeadCount(herd.Select(MortalityMath.Residency), From, To);
        var deaths = herd.Count(a => MortalityMath.IsMortality(a, null, From, To));

        avg.Should().Be(9.5m, "nine animals all month plus one for the 15 days before it died: 285 head-days ÷ 30");
        deaths.Should().Be(1);
        MortalityMath.Rate(deaths, avg).Should().Be(decimal.Round(1m / 9.5m, 6));
        MortalityMath.Rate(deaths, avg).Should().NotBe(0.1m, "the opening head count (10) is not the denominator");
    }

    [Fact]
    public void Slaughter_is_not_mortality_and_no_herd_means_no_rate()
    {
        var animal = NewAnimal("S-1");
        animal.RecordDeath(DisposalReason.Slaughter, new DateOnly(2026, 9, 5), null);

        MortalityMath.IsMortality(animal, null, From, To).Should().BeFalse();
        MortalityMath.Rate(0, 0m).Should().BeNull();
    }

    private static Animal NewAnimal(string tag) =>
        Animal.Create(TenantId, FarmId, AnimalTag.Create(tag, TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 1, 1), 50_000m, null);
}

public class SalesRegisterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    [Fact]
    public void Profit_is_price_less_cost_to_date_and_price_per_kg_uses_the_sale_weight()
    {
        var animal = NewAnimal();
        animal.Sell(120_000m, new DateOnly(2026, 11, 10), Guid.NewGuid(), "Abdul", 400m);

        var row = SalesRegisterReportDefinition.AnimalSaleRow("1", animal, "Sahiwal", 95_500m, null, ReportLanguage.En);

        row.PriceBdt.Should().Be(120_000m);
        row.CostToDateBdt.Should().Be(95_500m);
        row.ProfitBdt.Should().Be(24_500m);
        row.PricePerKgBdt.Should().Be(300m);
        row.Buyer.Should().Be("Abdul");
        row.Terms.Should().Be("Cash");
        row.OutstandingBdt.Should().Be(0m);
    }

    [Fact]
    public void A_loss_is_negative_and_a_credit_sale_shows_what_the_buyer_still_owes()
    {
        var animal = NewAnimal();
        animal.Sell(80_000m, new DateOnly(2026, 11, 10), Guid.NewGuid(), null, null);
        var bill = TradeBill.Create(TenantId, FarmId, TradeBillKind.Receivable, "Walk-in", animal.Id, TransactionCategory.AnimalSale,
            80_000m, new DateOnly(2026, 11, 10), null, "INV-1", "sale");
        bill.ApplyPayment(30_000m);

        var row = SalesRegisterReportDefinition.AnimalSaleRow("1", animal, "Local", 90_000m, bill, ReportLanguage.En);

        row.ProfitBdt.Should().Be(-10_000m);
        row.PricePerKgBdt.Should().BeNull("no sale weight was recorded, so no rate is invented");
        row.Terms.Should().Be("Credit");
        row.OutstandingBdt.Should().Be(50_000m);
    }

    private static Animal NewAnimal() =>
        Animal.Create(TenantId, FarmId, AnimalTag.Create("BD-0042", TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 1, 1), 60_000m, null);
}

public class TreatmentWithdrawalTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    [Fact]
    public void Withdrawal_runs_from_the_last_dose()
    {
        var p = Withdrawal.Evaluate(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), TreatmentStatus.Completed, 14, 3, Today);

        p.MeatClearDate.Should().Be(new DateOnly(2026, 11, 19));
        p.MeatWithheld.Should().BeTrue();
        p.MilkClearDate.Should().Be(new DateOnly(2026, 11, 8));
        p.MilkWithheld.Should().BeFalse();
    }

    [Fact]
    public void A_running_course_without_an_end_date_is_withheld()
    {
        var p = Withdrawal.Evaluate(new DateOnly(2026, 11, 1), null, TreatmentStatus.Ongoing, 7, 0, Today);

        p.LastDoseDate.Should().BeNull();
        p.MeatWithheld.Should().BeTrue();
        p.MilkWithheld.Should().BeFalse();
    }
}
