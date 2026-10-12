using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

public class IncomeExpenseReportQueryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IFinancialTransactionRepository> _txRepo = new();

    private static FinancialTransaction Tx(TransactionType type, TransactionCategory category, decimal amount, DateTime date) =>
        FinancialTransaction.Create(TenantId, FarmId, type, category, amount, date, description: category.ToString());

    private void Ledger(params FinancialTransaction[] txs) =>
        _txRepo.Setup(r => r.GetAllByFarmIdAsync(FarmId, It.IsAny<CancellationToken>())).ReturnsAsync(txs);

    [Fact]
    public async Task SingleDay_ComputesOpeningClosingAndExcludesOtherDays()
    {
        Ledger(
            Tx(TransactionType.Income, TransactionCategory.InvestorCapital, 10_000m, Day.AddDays(-3)),
            Tx(TransactionType.Expense, TransactionCategory.FeedCost, 2_000m, Day.AddDays(-1)),
            Tx(TransactionType.Income, TransactionCategory.MilkSale, 1_500m, Day.AddHours(9)),
            Tx(TransactionType.Expense, TransactionCategory.LaborCost, 700m, Day.AddHours(17)),
            Tx(TransactionType.Expense, TransactionCategory.Utilities, 999m, Day.AddDays(1)));

        var handler = new GetIncomeExpenseReportQueryHandler(_txRepo.Object);
        var result = await handler.Handle(new GetIncomeExpenseReportQuery(FarmId, Day, Day), CancellationToken.None);

        result.IsSingleDay.Should().BeTrue();
        result.OpeningBalanceBdt.Should().Be(8_000m);
        result.TotalIncomeBdt.Should().Be(1_500m);
        result.TotalExpenseBdt.Should().Be(700m);
        result.ClosingBalanceBdt.Should().Be(8_800m);
        result.IncomeTransactions.Should().ContainSingle().Which.Code.Should().Be("4020");
        result.ExpenseHeads.Should().ContainSingle().Which.Code.Should().Be("5040");
        result.DailyBreakdown.Should().ContainSingle().Which.ClosingBalanceBdt.Should().Be(8_800m);
    }

    [Fact]
    public async Task CapitalMoney_MovesCashButNotOperatingProfit()
    {
        Ledger(
            Tx(TransactionType.Income, TransactionCategory.InvestorCapital, 50_000m, Day),
            Tx(TransactionType.Income, TransactionCategory.AnimalSale, 30_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.LaborCost, 20_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.AnimalPurchase, 15_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.ProfitDistribution, 5_000m, Day));

        var handler = new GetIncomeExpenseReportQueryHandler(_txRepo.Object);
        var result = await handler.Handle(new GetIncomeExpenseReportQuery(FarmId, Day, Day), CancellationToken.None);

        result.NetBalanceBdt.Should().Be(40_000m);
        result.OperatingIncomeBdt.Should().Be(30_000m);
        result.OperatingExpenseBdt.Should().Be(20_000m);
        result.NetOperatingProfitBdt.Should().Be(10_000m);
        result.IncomeHeads.Single(h => h.Code == "3010").Group.Should().Be("Capital");
        // Buying an animal is cash out of the cash book but an asset, not an operating expense.
        result.ExpenseHeads.Single(h => h.Category == "AnimalPurchase").Group.Should().Be("Asset");
    }

    [Fact]
    public async Task DateRange_DailyBreakdownChainsBalancesAcrossDays()
    {
        Ledger(
            Tx(TransactionType.Income, TransactionCategory.OtherIncome, 1_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.Transport, 300m, Day.AddDays(2)));

        var handler = new GetIncomeExpenseReportQueryHandler(_txRepo.Object);
        var result = await handler.Handle(new GetIncomeExpenseReportQuery(FarmId, Day, Day.AddDays(2)), CancellationToken.None);

        result.IsSingleDay.Should().BeFalse();
        result.DailyBreakdown.Should().HaveCount(3);
        result.DailyBreakdown.Select(d => d.ClosingBalanceBdt).Should().Equal(1_000m, 1_000m, 700m);
        result.DailyBreakdown[1].OpeningBalanceBdt.Should().Be(1_000m);
        result.ClosingBalanceBdt.Should().Be(700m);
    }

    [Fact]
    public async Task FullAccounts_SplitsLabourAndReconcilesCash()
    {
        Ledger(
            Tx(TransactionType.Income, TransactionCategory.InvestorCapital, 100_000m, Day.AddDays(-5)),
            Tx(TransactionType.Income, TransactionCategory.AnimalSale, 40_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.LaborCost, 6_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.FeedCost, 4_000m, Day),
            Tx(TransactionType.Expense, TransactionCategory.LoanRepayment, 2_000m, Day));

        var investorRepo = new Mock<IInvestorRepository>();
        investorRepo.Setup(r => r.GetByFarmIdAsync(FarmId, true, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Investor>());
        var shareRepo = new Mock<IFarmShareRepository>();
        shareRepo.Setup(r => r.GetHoldingsByFarmIdAsync(FarmId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ShareHolding>());
        shareRepo.Setup(r => r.GetTransactionsByFarmIdAsync(FarmId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ShareTransaction>());
        var loanRepo = new Mock<ILoanRecordRepository>();
        loanRepo.Setup(r => r.GetByFarmIdAsync(FarmId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<LoanRecord>());
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<GetInvestorPnLQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestorPnLSummaryDto(FarmId, Day, Day, 40_000m, 10_000m, 30_000m, 5m, 1_500m, 28_500m, 0m, 0m, 0m, []));

        var handler = new GetFullAccountsReportQueryHandler(_txRepo.Object, investorRepo.Object, shareRepo.Object, loanRepo.Object, sender.Object);
        var result = await handler.Handle(new GetFullAccountsReportQuery(FarmId, Day, Day), CancellationToken.None);

        result.Cash.OpeningBalanceBdt.Should().Be(100_000m);
        result.Cash.ClosingBalanceBdt.Should().Be(128_000m);
        result.Operating.NetOperatingProfitBdt.Should().Be(30_000m);
        result.Costs.LabourCostBdt.Should().Be(6_000m);
        result.Costs.FeedCostBdt.Should().Be(4_000m);
        result.Capital.LoanRepaidBdt.Should().Be(2_000m);
        result.Ledger.Should().HaveCount(4);
        result.Ledger[^1].RunningBalanceBdt.Should().Be(result.Cash.ClosingBalanceBdt);
        result.ProfitShare.RetainedProfitBdt.Should().Be(28_500m);
    }
}
