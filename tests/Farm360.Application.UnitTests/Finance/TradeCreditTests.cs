using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>Payables and receivables: deals on credit, payments, write-offs and returns.</summary>
public class TradeCreditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 9, 1);
    private static readonly DateOnly Date = DateOnly.FromDateTime(Day);

    private readonly InMemoryFinancialTransactionRepository _ledger = new();
    private readonly InMemoryTradeBillRepository _bills = new();
    private readonly LedgerPostingService _posting;
    private readonly TradeCreditService _credit;

    public TradeCreditTests()
    {
        _posting = new LedgerPostingService(_ledger, _bills);
        _credit = new TradeCreditService(_bills, _ledger, _posting);
    }

    private async Task<FinancialTransaction> Manual(TransactionCategory category, decimal amount, DateTime? date = null)
    {
        var t = FinancialTransaction.Create(TenantId, FarmId, AccountChart.For(category).Type, category, amount, date ?? Day);
        await _ledger.AddAsync(t);
        return t;
    }

    private Dictionary<string, decimal> Balances => AccountChart.Balances(_ledger.Rows);

    [Fact]
    public async Task CreditPurchase_IsACostOnTheDay_ButCashMovesOnlyWhenPaid()
    {
        var feed = await Manual(TransactionCategory.FeedCost, 10_000m);

        var bill = await _credit.OpenAsync(feed, new CreditTerms("Rahim Feeds", PaidNowBdt: 4_000m));

        feed.Settlement.Should().Be(SettlementAccount.Payable);
        bill.Kind.Should().Be(TradeBillKind.Payable);
        bill.DueDate.Should().Be(Date.AddDays(TradeCreditService.DefaultCreditDays));
        bill.Status.Should().Be(TradeBillStatus.PartiallyPaid);
        AccountChart.Expenses(_ledger.Rows).Should().Be(10_000m, "the feed is a cost when bought, paid or not");
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(-4_000m, "only the advance left the cash box");
        (-Balances[AccountChart.PayableAccount]).Should().Be(6_000m).And.Be(bill.OutstandingBdt);

        await _credit.RecordPaymentAsync(bill, 6_000m, Date.AddDays(10), "bKash", "TX-1", null);

        bill.Status.Should().Be(TradeBillStatus.Paid);
        Balances.GetValueOrDefault(AccountChart.PayableAccount).Should().Be(0m);
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(-10_000m);
        AccountChart.Expenses(_ledger.Rows).Should().Be(10_000m, "paying a bill is not a second expense");
        Balances.Values.Sum().Should().Be(0m);
    }

    [Fact]
    public async Task Overpayment_IsRejected()
    {
        var bill = await _credit.OpenAsync(await Manual(TransactionCategory.LaborCost, 5_000m), new CreditTerms("Karim"));

        var act = () => _credit.RecordPaymentAsync(bill, 5_001m, Date, null, null, null);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*more than*outstanding*");
    }

    [Fact]
    public async Task CashOnlyHeads_CannotBeOnCredit()
    {
        var act = async () => await _credit.OpenAsync(await Manual(TransactionCategory.OwnerDrawing, 1_000m), new CreditTerms("Owner"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be put on credit*");
    }

    [Fact]
    public async Task CreditSale_FollowsPriceCorrections_AndBadDebtClearsTheRest()
    {
        var animalId = Guid.NewGuid();
        LedgerPosting Sale(decimal price) => new(TenantId, FarmId, TransactionCategory.AnimalSale, price, Day, animalId, "Livestock", "Sale of A-1");

        await _posting.PostAsync(Sale(90_000m));
        var bill = await _credit.OpenForSourceAsync(FarmId, animalId, TransactionCategory.AnimalSale, new CreditTerms("Hat Bazar Trader"));

        // The sale is corrected by the livestock module: the posting stays on credit and the bill follows.
        await _posting.PostAsync(Sale(95_000m));
        var sale = _ledger.Of(TransactionCategory.AnimalSale).Single();
        sale.Settlement.Should().Be(SettlementAccount.Receivable);
        bill.AmountBdt.Should().Be(95_000m);
        AccountChart.Revenue(_ledger.Rows).Should().Be(95_000m);
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(0m);

        await _credit.RecordPaymentAsync(bill, 50_000m, Date.AddDays(5), "Cash", null, null);
        await _credit.WriteOffAsync(bill, null, Date.AddDays(120), "Trader disappeared");

        bill.OutstandingBdt.Should().Be(0m);
        _ledger.Of(TransactionCategory.BadDebt).Single().AmountBdt.Should().Be(45_000m);
        Balances.GetValueOrDefault(AccountChart.ReceivableAccount).Should().Be(0m);
        AccountChart.NetProfit(_ledger.Rows).Should().Be(50_000m, "95,000 sold less 45,000 never collected");
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(50_000m);
    }

    [Fact]
    public async Task VoidingAPayment_ReversesItsCashPosting()
    {
        var bill = await _credit.OpenAsync(await Manual(TransactionCategory.MilkSale, 3_000m), new CreditTerms("Milk Vita"));
        var payment = await _credit.RecordPaymentAsync(bill, 3_000m, Date, null, null, null);
        _ledger.Of(TransactionCategory.CustomerReceipt).Should().ContainSingle();

        await _credit.VoidPaymentAsync(bill, payment);

        _ledger.Of(TransactionCategory.CustomerReceipt).Should().BeEmpty();
        bill.Status.Should().Be(TradeBillStatus.Open);
        Balances[AccountChart.ReceivableAccount].Should().Be(3_000m);
    }

    [Fact]
    public async Task CancellingAnUnpaidBill_PutsTheDealBackOnCash()
    {
        var transport = await Manual(TransactionCategory.Transport, 2_000m);
        var bill = await _credit.OpenAsync(transport, new CreditTerms("Truck owner"));

        await _credit.CancelAsync(bill);

        transport.Settlement.Should().Be(SettlementAccount.Cash);
        bill.Status.Should().Be(TradeBillStatus.Cancelled);
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(-2_000m);
    }

    [Fact]
    public async Task RemovedSource_WithPayments_LeavesAnAdvanceToRecover()
    {
        var poId = Guid.NewGuid();
        LedgerPosting Purchase(decimal amount) => new(TenantId, FarmId, TransactionCategory.InventoryPurchase, amount, Day, poId, "Inventory", "PO-1");
        await _posting.PostAsync(Purchase(20_000m));
        var bill = await _credit.OpenForSourceAsync(FarmId, poId, TransactionCategory.InventoryPurchase, new CreditTerms("Supplier"));
        await _credit.RecordPaymentAsync(bill, 5_000m, Date, null, null, null);

        await _posting.ReverseAsync(FarmId, poId, TransactionCategory.InventoryPurchase);

        bill.Status.Should().Be(TradeBillStatus.Overpaid);
        bill.OutstandingBdt.Should().Be(-5_000m);
        Balances[AccountChart.PayableAccount].Should().Be(5_000m, "a debit balance on payables is an advance to the supplier");
    }

    [Fact]
    public async Task ReturnAgainstACreditOrder_ReducesTheBill_NotCash()
    {
        var poId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.InventoryPurchase, 20_000m, Day, poId, "Inventory", "PO-1"));
        var bill = await _credit.OpenForSourceAsync(FarmId, poId, TransactionCategory.InventoryPurchase, new CreditTerms("Supplier"));

        // Re-running the same return must not deduct twice.
        for (var i = 0; i < 2; i++)
        {
            var settlement = await _credit.SettleReturnAsync(FarmId, poId, returnId, 3_000m);
            await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.InventoryPurchaseReturn, 3_000m, Day, returnId, "Inventory", "PR-1", Settlement: settlement));
        }

        bill.OutstandingBdt.Should().Be(17_000m);
        (-Balances[AccountChart.PayableAccount]).Should().Be(17_000m);
        Balances[AccountChart.InventoryAccount].Should().Be(17_000m);
        _ledger.Rows.Sum(AccountChart.CashEffect).Should().Be(0m);
    }

    [Fact]
    public async Task BalanceSheet_ShowsReceivablesAndPayables_AndBalances()
    {
        await _credit.OpenAsync(await Manual(TransactionCategory.MilkSale, 8_000m), new CreditTerms("Dairy"));
        await _credit.OpenAsync(await Manual(TransactionCategory.Utilities, 1_500m), new CreditTerms("PDB"));
        await Manual(TransactionCategory.OwnerCapital, 10_000m);

        var reconciliation = Substitute.For<IAccountingReconciliationService>();
        reconciliation.ReconcileAsync(FarmId, Arg.Any<IReadOnlyCollection<FinancialTransaction>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReconciliationLineDto>());
        var sheet = await new GetBalanceSheetQueryHandler(new FinancialStatementEngine(_ledger), reconciliation)
            .Handle(new GetBalanceSheetQuery(FarmId, Day), CancellationToken.None);

        sheet.Assets.Lines.Single(l => l.LineCode == AccountChart.ReceivableAccount).AmountBdt.Should().Be(8_000m);
        sheet.Liabilities.Lines.Single(l => l.LineCode == AccountChart.PayableAccount).AmountBdt.Should().Be(1_500m);
        sheet.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task Overview_AgesOpenBills_AndReconcilesToTheLedger()
    {
        var today = DateTime.Today;
        var old = await _credit.OpenAsync(await Manual(TransactionCategory.MilkSale, 4_000m, today.AddDays(-200)), new CreditTerms("Dairy"));
        await _credit.OpenAsync(await Manual(TransactionCategory.FeedCost, 7_000m, today), new CreditTerms("Feeds Ltd", DueDate: DateOnly.FromDateTime(today).AddDays(3)));

        var overview = await new GetTradeCreditOverviewQueryHandler(_bills, _ledger)
            .Handle(new GetTradeCreditOverviewQuery(FarmId), CancellationToken.None);

        overview.Receivables.OutstandingBdt.Should().Be(4_000m);
        overview.Receivables.OverdueBdt.Should().Be(4_000m);
        overview.Receivables.IsReconciled.Should().BeTrue();
        overview.Receivables.Aging.Single(a => a.Key == "90+").AmountBdt.Should().Be(4_000m);
        overview.Payables.OutstandingBdt.Should().Be(7_000m);
        overview.Payables.DueThisWeekBdt.Should().Be(7_000m);
        overview.Payables.IsReconciled.Should().BeTrue();
        overview.Bills.Should().HaveCount(2);
        overview.Bills.Single(b => b.Id == old.Id).DaysOverdue.Should().BeGreaterThan(90);
    }
}
