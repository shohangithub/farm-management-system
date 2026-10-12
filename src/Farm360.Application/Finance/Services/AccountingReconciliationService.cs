using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Finance.Services;

/// <summary>
/// Proves the books: every balance-sheet account is checked against the register that backs it
/// (stock on hand, the herd, the loan register, the investor register), and anything recorded on
/// one side but not the other is listed as an issue — most of them fixable by the ledger repair tool.
/// </summary>
/// <remarks>
/// A double-entry trial balance always balances, so "debits = credits" alone proves nothing.
/// These reconciliations are what actually catch a missing or duplicated posting.
/// </remarks>
public interface IAccountingReconciliationService
{
    Task<AccountingHealthDto> CheckAsync(Guid farmId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationLineDto>> ReconcileAsync(Guid farmId, IReadOnlyCollection<FinancialTransaction> postings, CancellationToken cancellationToken = default);
}

public sealed class AccountingReconciliationService : IAccountingReconciliationService
{
    /// <summary>Differences below this (rounding on weighted average cost) are treated as reconciled.</summary>
    public const decimal Tolerance = 1m;

    private readonly IFinancialTransactionRepository _transactions;
    private readonly IInventoryItemRepository _inventory;
    private readonly IAnimalRepository _animals;
    private readonly ILoanRecordRepository _loans;
    private readonly IInvestorRepository _investors;
    private readonly ITradeBillRepository _bills;

    public AccountingReconciliationService(
        IFinancialTransactionRepository transactions,
        IInventoryItemRepository inventory,
        IAnimalRepository animals,
        ILoanRecordRepository loans,
        IInvestorRepository investors,
        ITradeBillRepository bills)
    {
        _transactions = transactions;
        _inventory = inventory;
        _animals = animals;
        _loans = loans;
        _investors = investors;
        _bills = bills;
    }

    public async Task<AccountingHealthDto> CheckAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        var postings = await _transactions.GetAllByFarmIdAsync(farmId, cancellationToken);
        var reconciliations = await ReconcileAsync(farmId, postings, cancellationToken);
        var issues = await FindIssuesAsync(farmId, postings, reconciliations, cancellationToken);

        var cash = postings.Sum(AccountChart.CashEffect);
        return new AccountingHealthDto(
            farmId,
            DateTime.UtcNow,
            reconciliations.All(r => r.IsReconciled) && issues.All(i => i.Severity != "Error"),
            Math.Round(cash, 2),
            reconciliations,
            issues);
    }

    public async Task<IReadOnlyList<ReconciliationLineDto>> ReconcileAsync(
        Guid farmId,
        IReadOnlyCollection<FinancialTransaction> postings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postings);
        var balances = AccountChart.Balances(postings);
        decimal Bal(string code) => balances.GetValueOrDefault(code);

        // Inventory: stock on hand at weighted average cost.
        var items = await _inventory.GetByFarmIdAsync(farmId, null, cancellationToken);
        var stockValue = items.Sum(i => i.CurrentStock * i.WeightedAverageCostBdt);

        // Livestock: purchase cost of animals still on the farm.
        var animals = await _animals.GetAllByFarmAsync(farmId, cancellationToken);
        var herdCost = animals.Where(OnFarm).Sum(PurchaseCost);

        // Loans: principal still owed per the loan register.
        var loans = await _loans.GetByFarmIdAsync(farmId, cancellationToken);
        var loanPrincipal = loans.Sum(l => l.OutstandingPrincipalBdt);

        // Investor capital per the investor register.
        var investors = await _investors.GetByFarmIdAsync(farmId, includeTransactions: false, cancellationToken: cancellationToken);
        var investorCapital = investors.Sum(i => i.TotalInvestedBdt - i.TotalWithdrawnBdt);

        // Credit book: what is still owed on supplier bills and customer invoices.
        var bills = await _bills.GetByFarmIdAsync(farmId, cancellationToken: cancellationToken);
        var payables = bills.Where(b => b.Kind == TradeBillKind.Payable).ToList();
        var receivables = bills.Where(b => b.Kind == TradeBillKind.Receivable).ToList();

        return
        [
            Line("inventory", AccountChart.InventoryAccount, Bal(AccountChart.InventoryAccount), stockValue,
                $"Inventory: {items.Count} item(s) at weighted average cost",
                "Stock recorded without a purchase entry (opening stock before this update, manual adjustments), or stock purchases entered by hand in Finance, make these differ. Use 'Adjust books to stock count' after a physical count."),
            Line("livestock", AccountChart.LivestockAccount, Bal(AccountChart.LivestockAccount), herdCost,
                $"Herd: {animals.Count(OnFarm)} animal(s) on farm at purchase cost",
                "Animal purchases entered by hand in Finance (instead of registering the animal), or sales/deaths not yet booked, make these differ. Run the ledger repair, then delete any manual duplicate purchase entries."),
            Line("loans", AccountChart.LoanPayableAccount, -Bal(AccountChart.LoanPayableAccount), loanPrincipal,
                $"Loan register: {loans.Count} loan(s), principal outstanding",
                "Loans recorded before disbursements were posted, or repayments booked without the interest split, make these differ. The ledger repair fixes both."),
            Line("investors", AccountChart.InvestorCapitalAccount, -Bal(AccountChart.InvestorCapitalAccount), investorCapital,
                $"Investor register: {investors.Count} investor(s), capital in less withdrawn",
                "Investor money entered directly in the ledger instead of through Investors / Share Market makes these differ."),
            Line("payables", AccountChart.PayableAccount, -Bal(AccountChart.PayableAccount), payables.Sum(b => b.OutstandingBdt),
                $"Supplier bills: {payables.Count(b => b.IsOpen)} unpaid",
                "Every credit purchase raises a bill and every supplier payment is posted with it, so these only differ if postings were removed by hand."),
            Line("receivables", AccountChart.ReceivableAccount, Bal(AccountChart.ReceivableAccount), receivables.Sum(b => b.OutstandingBdt),
                $"Customer invoices: {receivables.Count(b => b.IsOpen)} unpaid",
                "Every credit sale raises an invoice and every collection is posted with it, so these only differ if postings were removed by hand."),
        ];
    }

    private async Task<IReadOnlyList<AccountingIssueDto>> FindIssuesAsync(
        Guid farmId,
        IReadOnlyCollection<FinancialTransaction> postings,
        IReadOnlyList<ReconciliationLineDto> reconciliations,
        CancellationToken cancellationToken)
    {
        var issues = new List<AccountingIssueDto>();
        var bySource = postings
            .Where(t => t.SourceDocumentId.HasValue)
            .ToLookup(t => (t.SourceDocumentId!.Value, t.Category));

        // Loans never posted as received.
        var loans = await _loans.GetByFarmIdAsync(farmId, cancellationToken);
        var unposted = loans.Where(l => !bySource[(l.Id, TransactionCategory.LoanDisbursement)].Any()).ToList();
        if (unposted.Count > 0)
        {
            issues.Add(new AccountingIssueDto("loan-disbursement", "Error",
                "Loans missing from the cash book",
                "These loans are in the loan register but the money received was never posted, so cash is understated.",
                unposted.Count, unposted.Sum(l => l.PrincipalAmountBdt), true));
        }

        // Repayments booked as one amount (no interest split).
        var legacyRepayments = postings
            .Where(t => t.Category == TransactionCategory.LoanRepayment && t.SourceDocumentId is null)
            .ToList();
        if (legacyRepayments.Count > 0 && loans.Any(l => l.InterestRatePercent > 0))
        {
            issues.Add(new AccountingIssueDto("loan-interest-split", "Warning",
                "Loan repayments without interest split",
                "Older repayments were booked entirely as debt reduction; the interest part belongs in expenses.",
                legacyRepayments.Count, legacyRepayments.Sum(t => t.AmountBdt), true));
        }

        // Animals whose purchase, sale or death is not booked consistently.
        var animals = await _animals.GetAllByFarmAsync(farmId, cancellationToken);
        var purchaseMismatch = 0;
        var missingDisposal = 0;
        decimal disposalValue = 0m;
        foreach (var animal in animals)
        {
            var expected = PurchaseCost(animal);
            var purchase = bySource[(animal.Id, TransactionCategory.AnimalPurchase)].FirstOrDefault()
                           ?? postings.FirstOrDefault(t => t.AnimalId == animal.Id && t.Category == TransactionCategory.AnimalPurchase);
            var booked = purchase?.AmountBdt ?? 0m;
            if (booked != expected
                || (purchase is not null && purchase.TransactionDate.Date != animal.AcquisitionDate.ToDateTime(TimeOnly.MinValue)))
            {
                purchaseMismatch++;
            }

            var disposalCategory = animal.Status switch
            {
                AnimalStatus.Sold => TransactionCategory.CostOfAnimalsSold,
                AnimalStatus.Slaughtered => TransactionCategory.CostOfAnimalsSold,
                AnimalStatus.Dead when animal.DisposalReason == DisposalReason.Slaughter => TransactionCategory.CostOfAnimalsSold,
                AnimalStatus.Dead => TransactionCategory.LivestockLoss,
                _ => (TransactionCategory?)null,
            };
            if (disposalCategory is { } cat && expected > 0 && !bySource[(animal.Id, cat)].Any())
            {
                missingDisposal++;
                disposalValue += expected;
            }
        }

        if (purchaseMismatch > 0)
        {
            issues.Add(new AccountingIssueDto("animal-purchase", "Warning",
                "Animal purchases not matching the herd register",
                "Purchase postings missing, with a different price, or dated on the registration day instead of the acquisition date.",
                purchaseMismatch, 0m, true));
        }

        if (missingDisposal > 0)
        {
            issues.Add(new AccountingIssueDto("animal-disposal", "Error",
                "Sold or dead animals still carried as assets",
                "Their purchase cost was never released from the livestock asset, so profit is overstated and the herd value is too high.",
                missingDisposal, disposalValue, true));
        }

        // Hand-entered postings that belong to another module.
        var manualAssets = postings
            .Where(t => !t.IsAutomated && t.Category is TransactionCategory.AnimalPurchase or TransactionCategory.InventoryPurchase)
            .ToList();
        if (manualAssets.Count > 0)
        {
            issues.Add(new AccountingIssueDto("manual-asset-purchases", "Warning",
                "Stock or animal purchases entered by hand",
                "Entered in Finance rather than through Inventory or Livestock, so no stock/animal stands behind them. Review them: delete duplicates of entries already posted by those modules.",
                manualAssets.Count, manualAssets.Sum(t => t.AmountBdt), false));
        }

        // Customer balances long overdue: likely bad debts that overstate assets and profit.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var stale = (await _bills.GetByFarmIdAsync(farmId, TradeBillKind.Receivable, cancellationToken: cancellationToken))
            .Where(b => b.IsOpen && TradeBillMapping.DaysOverdue(b, today) > 90)
            .ToList();
        if (stale.Count > 0)
        {
            issues.Add(new AccountingIssueDto("stale-receivables", "Warning",
                "Customer balances more than 90 days overdue",
                "Follow these up in Payables & Receivables. If they will not be paid, write them off as bad debts so profit and assets are not overstated.",
                stale.Count, stale.Sum(b => b.OutstandingBdt), false));
        }

        var cash = postings.Sum(AccountChart.CashEffect);
        if (cash < 0)
        {
            issues.Add(new AccountingIssueDto("negative-cash", "Warning",
                "Cash balance is negative",
                "More money has been recorded as paid out than received. Usually an opening cash balance, owner capital or loan receipt is missing.",
                1, Math.Round(-cash, 2), false));
        }

        foreach (var line in reconciliations.Where(r => !r.IsReconciled))
        {
            issues.Add(new AccountingIssueDto($"reconcile-{line.Key}", "Warning",
                $"{line.Account} does not agree with its register",
                line.Explanation,
                1, Math.Abs(line.DifferenceBdt), line.Key == "inventory"));
        }

        return issues;
    }

    private static ReconciliationLineDto Line(string key, string code, decimal books, decimal register, string source, string explanation)
    {
        var account = AccountChart.Account(code);
        var difference = Math.Round(books - register, 2);
        return new ReconciliationLineDto(
            key, code, account.Name, account.NameBn,
            Math.Round(books, 2), Math.Round(register, 2), difference,
            Math.Abs(difference) <= Tolerance, source, explanation);
    }

    public static bool OnFarm(Animal animal) =>
        animal.Status is not (AnimalStatus.Sold or AnimalStatus.Dead or AnimalStatus.Slaughtered);

    public static decimal PurchaseCost(Animal animal) =>
        animal.AcquisitionType == AcquisitionType.BornOnFarm ? 0m : animal.AcquisitionPriceBdt ?? 0m;
}
