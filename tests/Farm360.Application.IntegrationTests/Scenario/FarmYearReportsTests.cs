using System.Text.Json;
using System.Text.Json.Nodes;
using Farm360.Application.Finance.Queries;
using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Farm360.Application.IntegrationTests.Scenario;

/// <summary>
/// One fiscal year of a cattle-fattening partnership (<see cref="FarmYearScenario"/>), driven through
/// the real commands, then every finance report and page query checked against figures worked out by
/// hand — and against each other.
/// </summary>
/// <remarks>
/// <code>
/// Fiscal year 1 Jul 2025 – 30 Jun 2026 (books start 30 Jun 2025: opening cash 10,000 / owner's capital 10,000).
///
/// EVENT                                        CASH IN HAND   BANK       NOTE
/// opening                                      10,000         0
/// 01 Jul  partner A deposits                                  +300,000
/// 01 Jul  chopper & fans (straight line, 2 y)                 −24,000    depreciation 12,000 for the year
/// 05 Jul  transfer bank → cash                 +50,000        −50,000    no profit, total cash unchanged
/// 10 Jul  A1 bought (bank)                                    −60,000
/// 10 Jul  A2 bought on credit 80,000, 30,000 paid −30,000                payable 50,000
/// 15 Jul  feed 1,000 kg × 30 (bank)                           −30,000
/// 15 Jul  medicine 10 × 500 on credit                                    payable 5,000 (never paid)
/// 01 Aug  loan 100,000 @ 10% flat                             +100,000
/// 01 Aug  A1, A2 fed 10 kg each                                          feed 600
/// 15 Aug  A1 treated, 2 vials from stock                                 medicine 1,000
/// 31 Aug  labour                               −12,000                 shared out: A1 6,000, A2 6,000
/// 01 Sep  A2's balance paid                                   −50,000
/// 30 Sep  electricity                                         −3,000
/// 01 Jan  partner B deposits                   +200,000
/// 10 Jan  A3 50,000 paid directly by B         +50,000 −50,000           B's capital +50,000; cash nets to 0
/// 01 Feb  loan instalment 22,000                              −22,000    principal 20,000, interest 2,000
/// 01 Feb  A1, A2, A3 fed                                                 feed 900
/// 15 Feb  A4 bought (cash)                     −40,000
/// 20 Feb  A1–A4 fed                                                      feed 1,200
/// 01 Mar  A1 sold 120,000 (bank)                              +120,000   cost released 60,000
/// 15 Mar  A2 sold 150,000 on credit, 40,000 now +40,000                  cost released 80,000
/// 01 Apr  A3 dies                                                        loss 50,000
/// 15 Apr  30,000 collected from A2's buyer                    +30,000    receivable left 80,000
/// CLOSING                                      218,000        311,000    total cash 529,000 (+519,000)
///
/// FEED & CARE PER ANIMAL   A1 900 feed + 1,000 medicine + 6,000 labour = 7,900 · A2 900 + 6,000 = 6,900 · A3 600 · A4 300
///
/// PROFIT & LOSS (rule "carry growing costs" ON)          OFF (A4's 300 stays a cost of this year)
///   Revenue: livestock sales               270,000       270,000
///   Cost of animals sold (5000)           −140,000      −140,000
///   Mortality loss (5005)                  −50,000       −50,000
///   Carried to next period (5010)             +300             —   (A4's feed, on the farm at year end)
///   Feed −2,700 · labour −12,000 · medicine −1,000
///   = Cost of sales                       −205,400      −205,700
///   Gross profit                            64,600        64,300
///   Utilities −3,000 · depreciation −12,000 = −15,000
///   Interest                                −2,000
///   NET PROFIT                              47,600        47,300
///
/// BALANCE SHEET 30 Jun 2026
///   Cash 529,000 · receivable 80,000 · inventory 31,300 (910 kg feed 27,300 + 8 vials 4,000)
///   Livestock at cost 40,000 (A4) · carried feed & care 300 (ON) · PP&E 24,000 − 12,000
///   = assets 692,600 (ON) / 692,300 (OFF)
///   Payable 5,000 · loan 80,000 = liabilities 85,000
///   Investor capital 550,000 · owner's capital 10,000 · profit 47,600 = equity 607,600
///   Trial balance (ledger only): debits = credits = 927,000
///
/// PARTNERS (capital-days over the 365-day year)
///   A 300,000 × 365                         = 109,500,000
///   B 200,000 × 181 + 50,000 × 172         =  44,800,000      total 154,300,000
///   charity 5% of 47,600 = 2,380 → distributable 45,220 → A 32,090.67, B 13,129.33
///
/// SOLD ANIMALS  A1 120,000 − (60,000 + 900 + 1,000 + 6,000) = 52,100 · A2 150,000 − (80,000 + 900 + 6,000) = 63,100
///
/// BATCH P&L "Eid 2026" (A1–A4, as of 30 Jun 2026)
///   Money earned 270,000 − total cost (67,900 + 86,900 + 50,600 + 40,300 = 245,700) = profit so far 24,300
///   A4 still on the farm, never weighed → valued at its purchase cost 40,000 (was 0 before)
///   Expected profit 270,000 + 40,000 − 245,700 = 64,300
/// </code>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class FarmYearReportsTests(SqlServerFixture fixture)
{
    private const string Fy = "2025-07-01..2026-06-30";
    private static readonly string[] BalanceSections = ["Assets", "Liabilities", "Equity"];
    private static readonly DateTime From = new(2025, 7, 1);
    private static readonly DateTime To = new(2026, 6, 30);

    [SqlServerFact]
    public async Task EveryReportMatchesTheHandWorkedYearAndEachOther()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        // ── Income statement ───────────────────────────────────────────────
        var income = J(await host.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false)));
        N(income, "Totals.RevenueBdt").Should().Be(270_000m);
        N(income, "Totals.CostOfSalesBdt").Should().Be(205_400m);
        N(income, "Totals.GrossProfitBdt").Should().Be(64_600m);
        N(income, "Totals.OperatingExpensesBdt").Should().Be(15_000m);
        N(income, "Totals.FinanceCostsBdt").Should().Be(2_000m);
        N(income, "Totals.NetProfitBdt").Should().Be(47_600m);
        Line(income, "5000").Should().Be(140_000m);
        Line(income, "5005").Should().Be(50_000m);
        Line(income, "5010").Should().Be(-300m);
        Line(income, "5020").Should().Be(2_700m);
        Line(income, "5090").Should().Be(1_000m);
        Line(income, "5040").Should().Be(12_000m);
        Line(income, "5050").Should().Be(3_000m);
        Line(income, "5400").Should().Be(12_000m);
        Line(income, "5300").Should().Be(2_000m);

        // ── Balance sheet ──────────────────────────────────────────────────
        var balance = J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, To)));
        N(balance, "IsBalanced").Should().Be(1m);
        N(balance, "TotalAssetsBdt").Should().Be(692_600m);
        N(balance, "TotalLiabilitiesAndEquityBdt").Should().Be(692_600m);
        N(balance, "CurrentYearProfitBdt").Should().Be(47_600m);
        var bs = BalanceLines(balance);
        bs["1010"].Should().Be(529_000m);
        bs["1100"].Should().Be(80_000m);
        bs["1200"].Should().Be(31_300m);
        bs["1300"].Should().Be(40_000m);
        bs["1310"].Should().Be(300m);
        bs["1500"].Should().Be(24_000m);
        bs["1590"].Should().Be(-12_000m);
        bs["2100"].Should().Be(5_000m);
        bs["2010"].Should().Be(80_000m);
        bs["3010"].Should().Be(550_000m);
        bs["3200"].Should().Be(10_000m);

        // ── Trial balance ──────────────────────────────────────────────────
        var trial = J(await host.SendAsync(new GetTrialBalanceQuery(s.FarmId, To)));
        N(trial, "TotalDebitBdt").Should().Be(927_000m);
        N(trial, "TotalCreditBdt").Should().Be(927_000m);
        N(trial, "IsBalanced").Should().Be(1m);

        // ── Net profit: every page and report says the same ────────────────
        var full = J(await host.SendAsync(new GetFullAccountsReportQuery(s.FarmId, From, To)));
        N(full, "Operating.NetOperatingProfitBdt").Should().Be(47_600m);
        N(full, "Operating.TotalRevenueBdt").Should().Be(270_000m);
        N(full, "Operating.TotalExpenseBdt").Should().Be(222_400m);
        N(full, "Costs.TotalOperatingCostBdt").Should().Be(222_400m);
        N(full, "Cash.ClosingBalanceBdt").Should().Be(529_000m);
        N(full, "Assets.InventoryClosingBdt").Should().Be(31_300m);
        N(full, "Assets.LivestockClosingBdt").Should().Be(40_000m);
        N(full, "Assets.LivestockCarriedClosingBdt").Should().Be(300m);
        N(full, "Loans.TotalOutstandingPrincipalBdt").Should().Be(80_000m);
        Sum(full, "Trend", "NetOperatingBdt").Should().Be(47_600m, "the period trend adds up to the period");

        var incomeExpense = J(await host.SendAsync(new GetIncomeExpenseReportQuery(s.FarmId, From, To)));
        N(incomeExpense, "NetProfitBdt").Should().Be(47_600m);
        N(incomeExpense, "ClosingBalanceBdt").Should().Be(529_000m);
        N(incomeExpense, "OpeningBalanceBdt").Should().Be(10_000m);

        var dashboard = J(await host.SendAsync(new GetFinancialDashboardQuery(s.FarmId)));
        N(dashboard, "NetProfitYtdBdt").Should().Be(47_600m, "the farm clock stands on the last day of the year");
        N(dashboard, "RevenueYtdBdt").Should().Be(270_000m);
        N(dashboard, "ExpensesYtdBdt").Should().Be(222_400m);

        var months = 0m;
        for (var m = new DateOnly(2025, 7, 1); m <= new DateOnly(2026, 6, 1); m = m.AddMonths(1))
            months += (await host.SendAsync(new GetMonthlyPnLReportQuery(s.FarmId, m.Year, m.Month))).NetProfitBdt;
        months.Should().Be(47_600m, "the twelve monthly P&Ls add up to the year");

        var investorPnl = J(await host.SendAsync(new GetInvestorPnLQuery(s.FarmId, From, To)));
        N(investorPnl, "NetFarmProfitBdt").Should().Be(47_600m);
        N(investorPnl, "DistributableProfitBdt").Should().Be(45_220m);

        // Report engine versions of the statements.
        (await GrandTotal(host, "finance.income-statement", "amount", ("farmId", s.FarmId), ("fromDate", "2025-07-01"), ("toDate", "2026-06-30")))
            .Should().Be(47_600m);
        var monthlyReports = 0m;
        for (var m = new DateOnly(2025, 7, 1); m <= new DateOnly(2026, 6, 1); m = m.AddMonths(1))
            monthlyReports += await GrandTotal(host, "finance.monthly-pnl", "amount", ("farmId", s.FarmId), ("year", m.Year), ("month", m.Month));
        monthlyReports.Should().Be(47_600m);
        (await RowValue(host, "finance.full-accounts", "item", "NET OPERATING PROFIT", "amount", ("farmId", s.FarmId), ("period", Fy)))
            .Should().Be(47_600m);
        var trialReport = await Run(host, "finance.trial-balance", ("farmId", s.FarmId), ("asOfDate", "2026-06-30"));
        Total(trialReport, "debit").Should().Be(927_000m);
        Total(trialReport, "credit").Should().Be(927_000m);

        // ── Cash: the cash flow ties to the change in cash; the cash books to the balance sheet ──
        (await GrandTotal(host, "finance.cash-flow", "flow", ("farmId", s.FarmId), ("period", Fy)))
            .Should().Be(519_000m, "closing 529,000 − opening 10,000");
        var cashBook = await host.SendAsync(new GetMoneyAccountStatementQuery(s.FarmId, s.CashAccountId, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        var bankBook = await host.SendAsync(new GetMoneyAccountStatementQuery(s.FarmId, s.BankAccountId, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        cashBook.OpeningBdt.Should().Be(10_000m, "the opening cash entry belongs to the default account");
        cashBook.ClosingBdt.Should().Be(218_000m);
        bankBook.ClosingBdt.Should().Be(311_000m);
        (cashBook.ClosingBdt + bankBook.ClosingBdt).Should().Be(bs["1010"]);
        var accounts = await host.SendAsync(new GetMoneyAccountsQuery(host.TenantId, s.FarmId));
        accounts.Sum(a => a.BalanceBdt).Should().Be(529_000m);
        var cashBookReport = await Run(host, "finance.money-account-cash-book", ("farmId", s.FarmId), ("accountId", s.BankAccountId), ("period", Fy));
        (Total(cashBookReport, "in") - Total(cashBookReport, "out")).Should().Be(311_000m);

        // ── Partners: time-weighted shares of the distributable profit ─────
        var allocation = J(await host.SendAsync(new GetPartnerProfitAllocationQuery(s.FarmId, FarmYearScenario.YearStart, FarmYearScenario.YearEnd)));
        N(allocation, "NetProfitBdt").Should().Be(47_600m);
        N(allocation, "CharityBdt").Should().Be(2_380m);
        N(allocation, "DistributableProfitBdt").Should().Be(45_220m);
        var a = await host.SendAsync(new GetPartnerStatementQuery(s.FarmId, s.PartnerA, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        var b = await host.SendAsync(new GetPartnerStatementQuery(s.FarmId, s.PartnerB, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        a.Partner.ProfitAllocatedBdt.Should().Be(32_090.67m);
        b.Partner.ProfitAllocatedBdt.Should().Be(13_129.33m);
        (a.Partner.ProfitAllocatedBdt + b.Partner.ProfitAllocatedBdt).Should().Be(45_220m);
        a.ClosingCapitalBdt.Should().Be(300_000m);
        // "This calendar year" runs to 31 Dec 2026, but the farm clock stands on 30 Jun 2026: the
        // profit-allocation line is dated today, never in the future.
        var calendar = await host.SendAsync(new GetPartnerStatementQuery(s.FarmId, s.PartnerA, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        calendar.Lines.Where(l => l.Type == "ProfitAllocation").Should().ContainSingle()
            .Which.Date.Should().Be(new DateTime(2026, 6, 30));
        b.ClosingCapitalBdt.Should().Be(250_000m, "200,000 deposited + 50,000 paid directly for A3");
        (a.ClosingCapitalBdt + b.ClosingCapitalBdt).Should().Be(bs["3010"]);

        // ── Per animal: sold-animals P&L, cost ledger and the ledger's cost of sales ──
        var a1 = await host.SendAsync(new GetAnimalCostLedgerQuery(s.A1));
        var a2 = await host.SendAsync(new GetAnimalCostLedgerQuery(s.A2));
        a1.TotalCostBdt.Should().Be(67_900m);
        a1.ProfitLossBdt.Should().Be(52_100m);
        a2.TotalCostBdt.Should().Be(86_900m);
        a2.ProfitLossBdt.Should().Be(63_100m);
        (await GrandTotal(host, "finance.animal-cost-ledger", "amount", ("animalId", s.A1))).Should().Be(67_900m);
        var sold = await Run(host, "finance.sold-animals-profit", ("farmId", s.FarmId), ("period", Fy));
        Total(sold, "purchase").Should().Be(Line(income, "5000"), "the purchase cost of animals sold is the ledger's cost of animals sold");
        Total(sold, "profit").Should().Be(52_100m + 63_100m);
        Total(sold, "labour").Should().Be(12_000m, "August's wages, shared over A1 and A2");
        Total(sold, "price").Should().Be(270_000m);
        Row(sold, "animal", "A-001", "profit").Should().Be(52_100m);
        Row(sold, "animal", "A-002", "profit").Should().Be(63_100m);
        (Row(sold, "animal", "A-001", "feed") + Row(sold, "animal", "A-002", "feed")).Should().Be(1_800m);
        Row(sold, "animal", "A-001", "vet").Should().Be(1_000m);

        // ── Batch P&L: A4 has no weight yet, so it is carried at its purchase cost, not at 0 ──
        var batch = await host.SendAsync(new GetBatchPnLReportQuery(s.FarmId, s.BatchId));
        batch.TotalIncomeBdt.Should().Be(270_000m);
        batch.TotalCostBdt.Should().Be(245_700m);
        batch.GrossProfitBdt.Should().Be(24_300m);
        batch.OnHandAnimals.Should().Be(1);
        batch.UnweighedAtCostAnimals.Should().Be(1);
        batch.UnrealisedValueBdt.Should().Be(40_000m);
        batch.EstimatedTotalProfitBdt.Should().Be(64_300m);
        (await RowValue(host, "finance.batch-pnl", "lineItem", "Expected profit", "amount", ("farmId", s.FarmId), ("batchId", s.BatchId)))
            .Should().Be(64_300m);
    }

    [SqlServerFact]
    public async Task WithTheCarryRuleOffFeedAndCareAreCostsOfTheYearTheyWereSpent()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();
        await using var off = host.WithCarryGrowingCosts(false);

        var income = J(await off.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false)));
        N(income, "Totals.NetProfitBdt").Should().Be(47_300m);
        N(income, "Totals.CostOfSalesBdt").Should().Be(205_700m);
        Line(income, "5010").Should().Be(0m);

        var balance = J(await off.SendAsync(new GetBalanceSheetQuery(s.FarmId, To)));
        N(balance, "IsBalanced").Should().Be(1m);
        N(balance, "TotalAssetsBdt").Should().Be(692_300m);
        BalanceLines(balance).GetValueOrDefault("1310").Should().Be(0m);

        var trial = J(await off.SendAsync(new GetTrialBalanceQuery(s.FarmId, To)));
        N(trial, "TotalDebitBdt").Should().Be(927_000m, "the trial balance never carries the statement-only lines");

        N(J(await off.SendAsync(new GetFullAccountsReportQuery(s.FarmId, From, To))), "Operating.NetOperatingProfitBdt").Should().Be(47_300m);
        N(J(await off.SendAsync(new GetFinancialDashboardQuery(s.FarmId))), "NetProfitYtdBdt").Should().Be(47_300m);
        var months = 0m;
        for (var m = new DateOnly(2025, 7, 1); m <= new DateOnly(2026, 6, 1); m = m.AddMonths(1))
            months += (await off.SendAsync(new GetMonthlyPnLReportQuery(s.FarmId, m.Year, m.Month))).NetProfitBdt;
        months.Should().Be(47_300m);

        var allocation = J(await off.SendAsync(new GetPartnerProfitAllocationQuery(s.FarmId, FarmYearScenario.YearStart, FarmYearScenario.YearEnd)));
        N(allocation, "DistributableProfitBdt").Should().Be(44_935m);
        var a = await off.SendAsync(new GetPartnerStatementQuery(s.FarmId, s.PartnerA, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        a.Partner.ProfitAllocatedBdt.Should().Be(31_888.42m);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    internal static JsonNode J(object dto) => JsonNode.Parse(JsonSerializer.Serialize(dto))!;

    /// <summary>A number at a dotted path; booleans read as 1/0.</summary>
    internal static decimal N(JsonNode node, string path)
    {
        JsonNode? cursor = node;
        foreach (var part in path.Split('.'))
            cursor = cursor?[part];
        cursor.Should().NotBeNull($"'{path}' should exist");
        return cursor!.GetValueKind() switch
        {
            JsonValueKind.True => 1m,
            JsonValueKind.False => 0m,
            _ => cursor.GetValue<decimal>(),
        };
    }

    internal static decimal Sum(JsonNode node, string arrayPath, string field)
    {
        JsonNode? cursor = node;
        foreach (var part in arrayPath.Split('.'))
            cursor = cursor?[part];
        return cursor!.AsArray().Sum(x => x![field]!.GetValue<decimal>());
    }

    /// <summary>An income-statement line by account code (0 when absent).</summary>
    internal static decimal Line(JsonNode income, string code) =>
        income["Sections"]!.AsArray()
            .SelectMany(sec => sec!["Lines"]!.AsArray())
            .Where(l => (string?)l!["AccountCode"] == code)
            .Sum(l => l!["AmountBdt"]!.GetValue<decimal>());

    internal static Dictionary<string, decimal> BalanceLines(JsonNode balance) =>
        BalanceSections
            .SelectMany(sec => balance[sec]!["Lines"]!.AsArray())
            .GroupBy(l => (string)l!["LineCode"]!)
            .ToDictionary(g => g.Key, g => g.Sum(l => l!["AmountBdt"]!.GetValue<decimal>()));

    internal static async Task<ReportDataSet> Run(ScenarioHost host, string key, params (string Name, object Value)[] parameters)
    {
        var values = parameters.ToDictionary(p => p.Name, p => (string?)Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture));
        return await host.InScopeAsync(sp => sp.GetRequiredService<IReportExecutionService>().RunAsync(new ReportRequest(key, values)));
    }

    internal static async Task<decimal> GrandTotal(ScenarioHost host, string key, string field, params (string Name, object Value)[] parameters) =>
        Total(await Run(host, key, parameters), field);

    internal static async Task<decimal> RowValue(ScenarioHost host, string key, string matchField, string matchText, string field,
        params (string Name, object Value)[] parameters) =>
        Row(await Run(host, key, parameters), matchField, matchText, field);

    internal static decimal Total(ReportDataSet data, string field)
    {
        var i = Index(data, field);
        return ToDecimal(data.GrandTotals[i].V);
    }

    internal static decimal Row(ReportDataSet data, string matchField, string matchText, string field)
    {
        var m = Index(data, matchField);
        var i = Index(data, field);
        var row = data.Rows.Single(r => (r.Cells[m].F ?? string.Empty).Contains(matchText, StringComparison.Ordinal));
        return ToDecimal(row.Cells[i].V);
    }

    private static int Index(ReportDataSet data, string field)
    {
        var i = data.Columns.ToList().FindIndex(c => c.Field == field);
        i.Should().BeGreaterThanOrEqualTo(0, $"{data.Key} should have a '{field}' column");
        return i;
    }

    private static decimal ToDecimal(object? v) =>
        v is null ? 0m : Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture);
}
