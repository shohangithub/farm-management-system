using Farm360.Application.Finance.Commands;
using Farm360.Application.Finance.Queries;
using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Application.Livestock.Commands;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Livestock.Enums;
using Farm360.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Farm360.Application.IntegrationTests.Scenario.FarmYearReportsTests;

namespace Farm360.Application.IntegrationTests.Scenario;

/// <summary>
/// The accounting edge rules, each starting from the hand-worked year in <see cref="FarmYearReportsTests"/>
/// (net profit 47,600, cash 529,000 = cash in hand 218,000 + bank 311,000, receivable 80,000).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class FarmYearEdgeRulesTests(SqlServerFixture fixture)
{
    private static readonly DateTime From = new(2025, 7, 1);
    private static readonly DateTime To = new(2026, 6, 30);

    [SqlServerFact]
    public async Task UndoingACreditSaleReversesRevenueCostReceivableAndTheMoneyCollected()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        await host.SendAsync(new ReverseAnimalExitCommand(s.A2, "Sold by mistake"));

        // Revenue 120,000 (A1) − COGS 60,000 − loss 50,000 − feed 2,700 − medicine 1,000 − labour 12,000
        // + carried 7,200 (A4 300 + A2 900 feed + 6,000 labour, both on the farm again) − utilities 3,000 − depreciation 12,000 − interest 2,000.
        var income = J(await host.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false)));
        N(income, "Totals.RevenueBdt").Should().Be(120_000m);
        Line(income, "5000").Should().Be(60_000m);
        Line(income, "5010").Should().Be(-7_200m);
        N(income, "Totals.NetProfitBdt").Should().Be(-15_500m);

        var balance = J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, To)));
        N(balance, "IsBalanced").Should().Be(1m);
        var bs = BalanceLines(balance);
        bs.GetValueOrDefault("1100").Should().Be(0m, "the buyer's bill is cancelled");
        bs["1010"].Should().Be(459_000m, "the 40,000 and 30,000 received from the buyer are voided");
        bs["1300"].Should().Be(120_000m, "A2 is back in the herd at cost");
        bs["1310"].Should().Be(7_200m);
        N(balance, "TotalAssetsBdt").Should().Be(629_500m);

        var accounts = await host.SendAsync(new GetMoneyAccountsQuery(host.TenantId, s.FarmId));
        accounts.Single(a => a.Id == s.CashAccountId).BalanceBdt.Should().Be(178_000m);
        accounts.Single(a => a.Id == s.BankAccountId).BalanceBdt.Should().Be(281_000m);

        N(J(await host.SendAsync(new GetTrialBalanceQuery(s.FarmId, To))), "IsBalanced").Should().Be(1m);
        (await host.SendAsync(new GetAnimalCostLedgerQuery(s.A2))).SaleRevenueBdt.Should().Be(0m);
    }

    [SqlServerFact]
    public async Task UndoingADeathPutsTheAnimalBackAtCostAndRemovesTheLoss()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        await host.SendAsync(new ReverseAnimalExitCommand(s.A3, "Recorded the wrong animal"));

        var income = J(await host.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false)));
        Line(income, "5005").Should().Be(0m);
        Line(income, "5010").Should().Be(-900m, "A3's 600 of feed is carried again, with A4's 300");
        N(income, "Totals.NetProfitBdt").Should().Be(47_600m + 50_000m + 600m);

        var balance = J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, To)));
        N(balance, "IsBalanced").Should().Be(1m);
        BalanceLines(balance)["1300"].Should().Be(90_000m);
        BalanceLines(balance)["1010"].Should().Be(529_000m, "a death moved no money, and neither does undoing it");
    }

    [SqlServerFact]
    public async Task PurchaseCostIsReleasedExactlyOnceEvenWhenThePriceIsCorrectedAfterTheSale()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        // A1's purchase price corrected from 60,000 to 65,000 after it was sold.
        var a1 = await host.InScopeAsync(sp => sp.GetRequiredService<ApplicationDbContext>().Animals.AsNoTracking().SingleAsync(a => a.Id == s.A1));
        await host.SendAsync(new UpdateAnimalCommand(a1.Id, a1.Tag.TagId, a1.Tag.TagType, a1.Species, a1.BreedId, a1.Sex, a1.DateOfBirth,
            a1.AcquisitionType, a1.AcquisitionDate, 65_000m, a1.Notes));

        var postings = await host.InScopeAsync(sp => sp.GetRequiredService<ApplicationDbContext>().FinancialTransactions.AsNoTracking()
            .Where(t => t.FarmId == s.FarmId && (t.Category == TransactionCategory.CostOfAnimalsSold || t.Category == TransactionCategory.LivestockLoss))
            .ToListAsync());
        postings.Where(t => t.SourceDocumentId == s.A1).Should().ContainSingle().Which.AmountBdt.Should().Be(65_000m);
        postings.Where(t => t.SourceDocumentId == s.A2).Should().ContainSingle().Which.AmountBdt.Should().Be(80_000m);
        postings.Where(t => t.SourceDocumentId == s.A3).Should().ContainSingle().Which.Category.Should().Be(TransactionCategory.LivestockLoss);

        var balance = J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, To)));
        N(balance, "IsBalanced").Should().Be(1m);
        BalanceLines(balance)["1300"].Should().Be(40_000m, "only A4 is left; every other animal's cost has left the livestock account exactly once");
        N(J(await host.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false))), "Totals.NetProfitBdt").Should().Be(42_600m);
    }

    [SqlServerFact]
    public async Task APartnerPaidSpendNetsCashToZeroAndAddsToTheirCapital()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        var cashBook = await host.SendAsync(new GetMoneyAccountStatementQuery(s.FarmId, s.CashAccountId, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 10)));
        cashBook.TotalInBdt.Should().Be(50_000m);
        cashBook.TotalOutBdt.Should().Be(50_000m);
        cashBook.ClosingBdt.Should().Be(cashBook.OpeningBdt);

        var b = await host.SendAsync(new GetPartnerStatementQuery(s.FarmId, s.PartnerB, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        b.Partner.DepositedBdt.Should().Be(250_000m);
    }

    [SqlServerFact]
    public async Task ATransferMovesMoneyBetweenAccountsButNotProfitOrTotalCashAndDeletingItRestoresBoth()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        var transfer = await host.SendAsync(new TransferBetweenAccountsCommand(host.TenantId, s.FarmId,
            new TransferBetweenAccountsRequest(s.CashAccountId, s.BankAccountId, 100_000m, new DateTime(2026, 5, 1))));

        var accounts = await host.SendAsync(new GetMoneyAccountsQuery(host.TenantId, s.FarmId));
        accounts.Single(a => a.Id == s.CashAccountId).BalanceBdt.Should().Be(118_000m);
        accounts.Single(a => a.Id == s.BankAccountId).BalanceBdt.Should().Be(411_000m);
        BalanceLines(J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, To))))["1010"].Should().Be(529_000m);
        N(J(await host.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false))), "Totals.NetProfitBdt").Should().Be(47_600m);
        (await GrandTotal(host, "finance.cash-flow", "flow", ("farmId", s.FarmId), ("period", "2025-07-01..2026-06-30"))).Should().Be(519_000m);

        await host.SendAsync(new DeleteMoneyTransferCommand(s.FarmId, transfer.OutTransactionId));
        accounts = await host.SendAsync(new GetMoneyAccountsQuery(host.TenantId, s.FarmId));
        accounts.Single(a => a.Id == s.CashAccountId).BalanceBdt.Should().Be(218_000m);
        accounts.Single(a => a.Id == s.BankAccountId).BalanceBdt.Should().Be(311_000m);
    }

    [SqlServerFact]
    public async Task VoidingACreditBillPaymentPutsTheDebtBackAndTakesTheMoneyOut()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        var billId = await s.BillOfAsync(s.A2, TradeBillKind.Receivable);
        var bill = await host.SendAsync(new GetTradeBillQuery(s.FarmId, billId));
        var collection = bill.Payments.Single(p => p.AmountBdt == 30_000m);
        await host.SendAsync(new VoidBillPaymentCommand(s.FarmId, billId, collection.Id));

        var balance = J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, To)));
        N(balance, "IsBalanced").Should().Be(1m);
        BalanceLines(balance)["1100"].Should().Be(110_000m);
        BalanceLines(balance)["1010"].Should().Be(499_000m);
        var accounts = await host.SendAsync(new GetMoneyAccountsQuery(host.TenantId, s.FarmId));
        accounts.Single(a => a.Id == s.BankAccountId).BalanceBdt.Should().Be(281_000m, "the 30,000 went into the bank");
        N(J(await host.SendAsync(new GetIncomeStatementQuery(s.FarmId, From, To, false))), "Totals.NetProfitBdt").Should().Be(47_600m);
        (await host.SendAsync(new GetTradeBillQuery(s.FarmId, billId))).OutstandingBdt.Should().Be(110_000m);
    }

    [SqlServerFact]
    public async Task TheOpeningBalancesOpenTheYearAndTheDefaultCashBook()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        var opening = J(await host.SendAsync(new GetBalanceSheetQuery(s.FarmId, FarmYearScenario.BooksStart.ToDateTime(TimeOnly.MinValue))));
        N(opening, "IsBalanced").Should().Be(1m);
        BalanceLines(opening)["1010"].Should().Be(10_000m);
        BalanceLines(opening)["3200"].Should().Be(10_000m);
        N(opening, "CurrentYearProfitBdt").Should().Be(0m);

        var cashBook = await host.SendAsync(new GetMoneyAccountStatementQuery(s.FarmId, s.CashAccountId, FarmYearScenario.YearStart, FarmYearScenario.YearEnd));
        cashBook.OpeningBdt.Should().Be(10_000m);
        var income = J(await host.SendAsync(new GetIncomeExpenseReportQuery(s.FarmId, From, To)));
        N(income, "OpeningBalanceBdt").Should().Be(10_000m);
    }

    [SqlServerFact]
    public async Task TheHealthCheckCountsTheOpeningCashSoItNeitherUnderstatesCashNorWarnsOfAnOverdraft()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.SetupAsync();

        // Opening cash 10,000 in cash in hand; 5,000 of wages paid from it.
        await host.SendAsync(new RecordExpenseCommand(host.TenantId, s.FarmId, "LaborCost", 5_000m, new DateTime(2025, 8, 31), "Wages", "W-1", "",
            null, null, null, MoneyAccountId: s.CashAccountId));

        var health = await host.SendAsync(new GetAccountingHealthQuery(s.FarmId));
        health.CashBalanceBdt.Should().Be(5_000m, "the balance sheet's cash: opening 10,000 − 5,000 paid");
        health.Issues.Should().NotContain(i => i.Key == "negative-cash", "cash in hand still holds 5,000");
        health.IsHealthy.Should().BeTrue();
    }

    [SqlServerFact]
    public async Task TheHealthCheckOfTheYearAgreesWithTheBalanceSheet()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        var health = await host.SendAsync(new GetAccountingHealthQuery(s.FarmId));
        health.CashBalanceBdt.Should().Be(529_000m);
        health.Reconciliations.Should().OnlyContain(r => r.IsReconciled);
        health.IsHealthy.Should().BeTrue();
    }
}
