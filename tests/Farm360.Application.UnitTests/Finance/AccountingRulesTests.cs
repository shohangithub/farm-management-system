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

/// <summary>The accounting policy, checked end to end on small, hand-computable books.</summary>
public class AccountingRulesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 10, 1);

    private static FinancialTransaction Post(TransactionCategory category, decimal amount,
        SettlementAccount settlement = SettlementAccount.Cash, DateTime? date = null) =>
        FinancialTransaction.Create(TenantId, FarmId, AccountChart.For(category).Type, category, amount, date ?? Day,
            settlement: settlement);

    // ── Inventory: bought is an asset, used is a cost ───────────────────────

    [Fact]
    public void StockPurchase_IsAnAsset_AndOnlyConsumptionReachesProfitAndLoss()
    {
        var books = new List<FinancialTransaction>
        {
            Post(TransactionCategory.OwnerCapital, 50_000m),                                // owner puts in cash
            Post(TransactionCategory.InventoryPurchase, 10_000m),                           // buy feed
            Post(TransactionCategory.FeedCost, 3_000m, SettlementAccount.Inventory),        // feed fed from stock
            Post(TransactionCategory.InventoryWriteOff, 500m, SettlementAccount.Inventory), // spoiled
        };

        var balances = AccountChart.Balances(books);
        balances[AccountChart.CashAccount].Should().Be(40_000m);
        balances[AccountChart.InventoryAccount].Should().Be(6_500m);
        AccountChart.Expenses(books).Should().Be(3_500m, "the purchase itself is not a cost; what was used and lost is");
        books.Sum(AccountChart.CashEffect).Should().Be(40_000m, "feeding from stock moves no cash");
        balances.Values.Sum().Should().Be(0m, "every posting is a complete double entry");
    }

    [Fact]
    public void CapitalAndLoans_MoveCash_ButAreNeverProfit()
    {
        var books = new List<FinancialTransaction>
        {
            Post(TransactionCategory.InvestorCapital, 100_000m),
            Post(TransactionCategory.LoanDisbursement, 50_000m),
            Post(TransactionCategory.LoanRepayment, 10_000m),
            Post(TransactionCategory.InterestExpense, 1_000m),
            Post(TransactionCategory.ProfitDistribution, 5_000m),
        };

        AccountChart.Revenue(books).Should().Be(0m);
        AccountChart.Expenses(books).Should().Be(1_000m, "only interest is a cost of borrowing");
        books.Sum(AccountChart.CashEffect).Should().Be(134_000m);
        AccountChart.Balances(books)[AccountChart.LoanPayableAccount].Should().Be(-40_000m, "40,000 principal still owed");
    }

    // ── Loans ───────────────────────────────────────────────────────────────

    [Fact]
    public void LoanRepayment_SplitsIntoPrincipalAndInterest_ProRata_AndSettlesExactly()
    {
        // 100,000 at 12% flat → 112,000 payable, 12,000 interest.
        var loan = LoanRecord.Create(TenantId, FarmId, "Krishi Bank", 100_000m, 12m, Day, RepaymentSchedule.Monthly);

        var (p1, i1) = loan.RecordRepayment(11_200m);
        p1.Should().Be(10_000m);
        i1.Should().Be(1_200m);
        loan.OutstandingPrincipalBdt.Should().Be(90_000m);

        var (p2, i2) = loan.RecordRepayment(100_800m); // the rest
        (p1 + p2).Should().Be(100_000m);
        (i1 + i2).Should().Be(12_000m);
        loan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void LoanRepayment_AboveWhatIsOwed_IsRejected()
    {
        var loan = LoanRecord.Create(TenantId, FarmId, "Krishi Bank", 10_000m, 10m, Day, RepaymentSchedule.LumpSum);

        var act = () => loan.RecordRepayment(12_000m);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Manual entry guards ─────────────────────────────────────────────────

    [Theory]
    [InlineData(TransactionType.Expense, TransactionCategory.InventoryPurchase)]
    [InlineData(TransactionType.Expense, TransactionCategory.AnimalPurchase)]
    [InlineData(TransactionType.Income, TransactionCategory.AnimalSale)]
    [InlineData(TransactionType.Income, TransactionCategory.LoanDisbursement)]
    [InlineData(TransactionType.Income, TransactionCategory.InvestorCapital)]
    [InlineData(TransactionType.Income, TransactionCategory.FeedCost)]
    public void ManualEntry_OfModuleOwnedOrWrongDirectionHeads_IsRejected(TransactionType type, TransactionCategory category)
    {
        var act = () => AccountChart.EnsureManualAllowed(type, category);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(TransactionType.Expense, TransactionCategory.LaborCost)]
    [InlineData(TransactionType.Expense, TransactionCategory.Utilities)]
    [InlineData(TransactionType.Income, TransactionCategory.MilkSale)]
    [InlineData(TransactionType.Income, TransactionCategory.OwnerCapital)]
    public void ManualEntry_OfOrdinaryHeads_IsAllowed(TransactionType type, TransactionCategory category)
    {
        var act = () => AccountChart.EnsureManualAllowed(type, category);
        act.Should().NotThrow();
    }

    [Fact]
    public void AutomatedPostings_CannotBeEditedFromTheLedger()
    {
        var automated = FinancialTransaction.Create(TenantId, FarmId, TransactionType.Expense, TransactionCategory.FeedCost,
            100m, Day, isAutomated: true, sourceModule: "Feeding");

        var act = () => AccountChart.EnsureEditable(automated);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Feeding*");
    }

    // ── Posting service ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostingService_IsIdempotent_RevisesAndReverses()
    {
        var ledger = new InMemoryFinancialTransactionRepository();
        var service = new LedgerPostingService(ledger);
        var source = Guid.NewGuid();
        LedgerPosting Posting(decimal amount) => new(TenantId, FarmId, TransactionCategory.VeterinaryCost, amount, Day, source, "Health", "Vet visit");

        await service.PostAsync(Posting(800m));
        await service.PostAsync(Posting(800m));
        ledger.Rows.Should().ContainSingle().Which.AmountBdt.Should().Be(800m);

        await service.PostAsync(Posting(950m));
        ledger.Rows.Should().ContainSingle().Which.AmountBdt.Should().Be(950m);

        await service.PostAsync(Posting(0m));
        ledger.Rows.Should().BeEmpty();
    }

    // ── Statements ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BalanceSheet_IsBuiltFromTheJournal_AndBalances()
    {
        var ledger = new InMemoryFinancialTransactionRepository();
        foreach (var t in new[]
                 {
                     Post(TransactionCategory.InvestorCapital, 200_000m),
                     Post(TransactionCategory.LoanDisbursement, 100_000m),
                     Post(TransactionCategory.AnimalPurchase, 150_000m),
                     Post(TransactionCategory.InventoryPurchase, 30_000m),
                     Post(TransactionCategory.FeedCost, 12_000m, SettlementAccount.Inventory),
                     Post(TransactionCategory.LaborCost, 8_000m),
                     Post(TransactionCategory.AnimalSale, 90_000m),
                     Post(TransactionCategory.CostOfAnimalsSold, 60_000m, SettlementAccount.Livestock),
                     Post(TransactionCategory.LoanRepayment, 20_000m),
                     Post(TransactionCategory.InterestExpense, 2_000m),
                 })
        {
            await ledger.AddAsync(t);
        }

        var reconciliation = Substitute.For<IAccountingReconciliationService>();
        reconciliation.ReconcileAsync(FarmId, Arg.Any<IReadOnlyCollection<FinancialTransaction>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReconciliationLineDto>());
        var handler = new GetBalanceSheetQueryHandler(ledger, reconciliation);

        var sheet = await handler.Handle(new GetBalanceSheetQuery(FarmId, Day), CancellationToken.None);

        Line(sheet.Assets, AccountChart.CashAccount).Should().Be(180_000m);
        Line(sheet.Assets, AccountChart.InventoryAccount).Should().Be(18_000m);
        Line(sheet.Assets, AccountChart.LivestockAccount).Should().Be(90_000m);
        Line(sheet.Liabilities, AccountChart.LoanPayableAccount).Should().Be(80_000m);
        Line(sheet.Equity, AccountChart.RetainedEarningsAccount).Should().Be(8_000m, "90,000 − 60,000 − 12,000 − 8,000 − 2,000");
        sheet.TotalAssetsBdt.Should().Be(288_000m);
        sheet.IsBalanced.Should().BeTrue();
        sheet.TotalLiabilitiesAndEquityBdt.Should().Be(sheet.TotalAssetsBdt);
    }

    private static decimal Line(BalanceSheetSectionDto section, string code) =>
        section.Lines.Single(l => l.LineCode == code).AmountBdt;
}
