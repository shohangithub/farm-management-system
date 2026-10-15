using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Application.Finance.Services;

/// <summary>Where an account lands on the financial statements.</summary>
public enum AccountClass
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense,
}

/// <summary>
/// The farm's chart of accounts and the single source of truth for how every ledger posting is
/// classified. Every report, dashboard and statement goes through here, so they cannot disagree.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="FinancialTransaction"/> is one complete double entry: its category names one
/// account, its <see cref="SettlementAccount"/> names the other. An Expense-type row debits the
/// category account and credits the settlement account; an Income-type row debits the settlement
/// account and credits the category account.
/// </para>
/// <para>
/// Accounting policy (modified accrual, the standard for livestock fattening operations):
/// <list type="bullet">
/// <item>Stock bought is an <b>Inventory asset</b>. It becomes an expense only when consumed
/// (feed fed, medicine given, consumables used) or written off.</item>
/// <item>Animals bought are a <b>Livestock asset</b> at cost. The cost becomes an expense
/// (<c>CostOfAnimalsSold</c>) when the animal is sold or slaughtered, or a <c>LivestockLoss</c>
/// when it dies — so a month in which cattle are bought is not shown as a huge loss.</item>
/// <item>Loans are liabilities. Only the interest part of a repayment is an expense.</item>
/// <item>Investor and owner money is equity — it moves cash but is never profit.</item>
/// <item>Purchases and sales on credit are recognised on the deal date against Accounts Payable /
/// Accounts Receivable; the cash book moves only when the supplier is paid or the customer pays.</item>
/// </list>
/// </para>
/// </remarks>
public static class AccountChart
{
    // Report groups (how a head is presented in cash-book style reports).
    public const string Operating = "Operating";
    public const string Asset = "Asset";
    public const string Capital = "Capital";
    public const string Financing = "Financing";
    public const string Trade = "Trade";

    // Cost-analysis buckets for the full accounts report.
    public const string CostLivestock = "livestock";
    public const string CostFeed = "feed";
    public const string CostHealth = "health";
    public const string CostLabour = "labour";
    public const string CostInventory = "inventory";
    public const string CostOverhead = "overhead";
    public const string CostFinance = "finance";

    // ── Ledger (balance) accounts ───────────────────────────────────────────
    public const string CashAccount = "1010";
    public const string ReceivableAccount = "1100";
    public const string InventoryAccount = "1200";
    public const string LivestockAccount = "1300";
    public const string PayableAccount = "2100";
    public const string LoanPayableAccount = "2010";
    public const string InvestorCapitalAccount = "3010";
    public const string ProfitDistributionAccount = "3110";
    public const string OwnerCapitalAccount = "3200";
    public const string OwnerDrawingAccount = "3210";
    public const string RetainedEarningsAccount = "3900";

    // Financial-statement accounts (fixed assets, accruals, opening position).
    public const string AdvancesDepositsAccount = "1150";
    public const string FixedAssetAccount = "1500";
    public const string AccumulatedDepreciationAccount = "1590";
    public const string ProvisionsAccount = "2200";
    public const string OtherLiabilitiesAccount = "2300";
    public const string DepreciationAccount = "5400";
    public const string AssetDisposalAccount = "5410";

    /// <summary>Statement-only line for the year's profit (not a ledger account).</summary>
    public const string CurrentYearProfitLine = "3950";

    public sealed record LedgerAccount(string Code, string Name, string NameBn, AccountClass Class);

    /// <param name="Code">The ledger account the head posts to.</param>
    /// <param name="Group">Presentation group: Operating, Asset, Capital or Financing.</param>
    /// <param name="Type">The natural direction of the head (Income credits it, Expense debits it).</param>
    public sealed record AccountHead(
        string Code,
        string Name,
        string NameBn,
        AccountClass Class,
        string Group,
        TransactionType Type,
        string? CostGroup = null);

    private static readonly Dictionary<TransactionCategory, AccountHead> Heads = new()
    {
        // ── Revenue ──
        [TransactionCategory.AnimalSale] = new("4010", "Livestock Sales", "পশু বিক্রয়", AccountClass.Revenue, Operating, TransactionType.Income),
        [TransactionCategory.MilkSale] = new("4020", "Milk & Dairy Sales", "দুধ বিক্রয়", AccountClass.Revenue, Operating, TransactionType.Income),
        [TransactionCategory.ByproductSale] = new("4030", "By-product & Manure Sales", "উপজাত বিক্রয়", AccountClass.Revenue, Operating, TransactionType.Income),
        [TransactionCategory.OtherIncome] = new("4040", "Other Farm Income", "অন্যান্য আয়", AccountClass.Revenue, Operating, TransactionType.Income),

        // ── Expenses ──
        [TransactionCategory.CostOfAnimalsSold] = new("5000", "Cost of Animals Sold", "বিক্রিত পশুর ক্রয়মূল্য", AccountClass.Expense, Operating, TransactionType.Expense, CostLivestock),
        [TransactionCategory.LivestockLoss] = new("5005", "Livestock Mortality Loss", "পশু মৃত্যুজনিত ক্ষতি", AccountClass.Expense, Operating, TransactionType.Expense, CostLivestock),
        [TransactionCategory.FeedCost] = new("5020", "Feed & Nutrition", "খাদ্য খরচ", AccountClass.Expense, Operating, TransactionType.Expense, CostFeed),
        [TransactionCategory.VeterinaryCost] = new("5030", "Veterinary & Healthcare", "পশু চিকিৎসা খরচ", AccountClass.Expense, Operating, TransactionType.Expense, CostHealth),
        [TransactionCategory.LaborCost] = new("5040", "Labour & Farm Wages", "শ্রমিক মজুরি", AccountClass.Expense, Operating, TransactionType.Expense, CostLabour),
        [TransactionCategory.Utilities] = new("5050", "Utilities & Energy", "বিদ্যুৎ ও ইউটিলিটি", AccountClass.Expense, Operating, TransactionType.Expense, CostOverhead),
        [TransactionCategory.Transport] = new("5060", "Transport & Logistics", "পরিবহন খরচ", AccountClass.Expense, Operating, TransactionType.Expense, CostOverhead),
        [TransactionCategory.MiscellaneousExpense] = new("5070", "Miscellaneous Expenses", "বিবিধ খরচ", AccountClass.Expense, Operating, TransactionType.Expense, CostOverhead),
        [TransactionCategory.MedicineCost] = new("5090", "Medicines & Vaccines", "ঔষধ ও টিকা", AccountClass.Expense, Operating, TransactionType.Expense, CostHealth),
        [TransactionCategory.ConsumableExpense] = new("5100", "Consumables & Daily Supplies", "দৈনিক ভোগ্যপণ্য", AccountClass.Expense, Operating, TransactionType.Expense, CostInventory),
        [TransactionCategory.InventoryWriteOff] = new("5110", "Stock Write-off & Shrinkage", "মজুদ ক্ষতি / বাতিল", AccountClass.Expense, Operating, TransactionType.Expense, CostInventory),
        [TransactionCategory.InterestExpense] = new("5300", "Loan Interest", "ঋণের সুদ", AccountClass.Expense, Operating, TransactionType.Expense, CostFinance),
        [TransactionCategory.BadDebt] = new("5310", "Bad Debts Written Off", "অনাদায়ী পাওনা", AccountClass.Expense, Operating, TransactionType.Expense, CostFinance),
        // Sale proceeds post as Income on this account and the net book value is derecognised
        // against it, so its balance is the loss (or, if negative, the gain) on disposal.
        [TransactionCategory.AssetDisposal] = new(AssetDisposalAccount, "Loss / (Gain) on Disposal of Fixed Assets", "স্থায়ী সম্পদ বিক্রয়ে ক্ষতি / (লাভ)", AccountClass.Expense, Operating, TransactionType.Income, CostOverhead),

        // ── Assets acquired / disposed ──
        [TransactionCategory.InventoryPurchase] = new(InventoryAccount, "Stock Purchase", "মালামাল ক্রয় (মজুদ)", AccountClass.Asset, Asset, TransactionType.Expense),
        [TransactionCategory.InventoryPurchaseReturn] = new(InventoryAccount, "Stock Returned to Supplier", "সরবরাহকারীকে ফেরত", AccountClass.Asset, Asset, TransactionType.Income),
        [TransactionCategory.AnimalPurchase] = new(LivestockAccount, "Livestock Purchase", "পশু ক্রয়", AccountClass.Asset, Asset, TransactionType.Expense),
        [TransactionCategory.FixedAssetPurchase] = new(FixedAssetAccount, "Fixed Asset Purchase", "স্থায়ী সম্পদ ক্রয়", AccountClass.Asset, Asset, TransactionType.Expense),

        // ── Credit settlement (payables & receivables) ──
        [TransactionCategory.SupplierPayment] = new(PayableAccount, "Supplier Payments (credit bills)", "সরবরাহকারীকে বকেয়া পরিশোধ", AccountClass.Liability, Trade, TransactionType.Expense),
        [TransactionCategory.CustomerReceipt] = new(ReceivableAccount, "Customer Collections (credit sales)", "ক্রেতার কাছ থেকে বকেয়া আদায়", AccountClass.Asset, Trade, TransactionType.Income),

        // ── Financing (loans) ──
        [TransactionCategory.LoanDisbursement] = new(LoanPayableAccount, "Loan Received", "ঋণ গ্রহণ", AccountClass.Liability, Financing, TransactionType.Income),
        [TransactionCategory.LoanRepayment] = new(LoanPayableAccount, "Loan Principal Repaid", "ঋণের আসল পরিশোধ", AccountClass.Liability, Financing, TransactionType.Expense),

        // ── Capital (investors & owner) ──
        [TransactionCategory.InvestorCapital] = new(InvestorCapitalAccount, "Investor Capital", "বিনিয়োগ মূলধন", AccountClass.Equity, Capital, TransactionType.Income),
        [TransactionCategory.InvestorWithdrawal] = new(InvestorCapitalAccount, "Investor Capital Withdrawal", "বিনিয়োগ মূলধন উত্তোলন", AccountClass.Equity, Capital, TransactionType.Expense),
        [TransactionCategory.ProfitDistribution] = new(ProfitDistributionAccount, "Profit Distribution", "লভ্যাংশ বণ্টন", AccountClass.Equity, Capital, TransactionType.Expense),
        [TransactionCategory.OwnerCapital] = new(OwnerCapitalAccount, "Owner's Capital", "মালিকের মূলধন", AccountClass.Equity, Capital, TransactionType.Income),
        [TransactionCategory.OwnerDrawing] = new(OwnerDrawingAccount, "Owner's Drawings", "মালিকের উত্তোলন", AccountClass.Equity, Capital, TransactionType.Expense),
    };

    private static readonly Dictionary<string, LedgerAccount> LedgerAccounts = new()
    {
        [CashAccount] = new(CashAccount, "Cash & Bank", "নগদ ও ব্যাংক", AccountClass.Asset),
        [ReceivableAccount] = new(ReceivableAccount, "Accounts Receivable (customers)", "প্রাপ্য হিসাব (ক্রেতা)", AccountClass.Asset),
        [PayableAccount] = new(PayableAccount, "Accounts Payable (suppliers)", "প্রদেয় হিসাব (সরবরাহকারী)", AccountClass.Liability),
        [InventoryAccount] = new(InventoryAccount, "Inventory (Feed, Medicine & Supplies)", "মজুদ মালামাল", AccountClass.Asset),
        [LivestockAccount] = new(LivestockAccount, "Livestock (at cost)", "গবাদিপশু (ক্রয়মূল্যে)", AccountClass.Asset),
        [LoanPayableAccount] = new(LoanPayableAccount, "Loans Payable", "প্রদেয় ঋণ", AccountClass.Liability),
        [InvestorCapitalAccount] = new(InvestorCapitalAccount, "Investor Capital", "বিনিয়োগকারীর মূলধন", AccountClass.Equity),
        [ProfitDistributionAccount] = new(ProfitDistributionAccount, "Profit Distributed to Investors", "বণ্টিত লভ্যাংশ", AccountClass.Equity),
        [OwnerCapitalAccount] = new(OwnerCapitalAccount, "Owner's Capital", "মালিকের মূলধন", AccountClass.Equity),
        [OwnerDrawingAccount] = new(OwnerDrawingAccount, "Owner's Drawings", "মালিকের উত্তোলন", AccountClass.Equity),
        [RetainedEarningsAccount] = new(RetainedEarningsAccount, "Retained Earnings", "সঞ্চিত মুনাফা", AccountClass.Equity),
        [AdvancesDepositsAccount] = new(AdvancesDepositsAccount, "Advances, Deposits & Prepayments", "অগ্রিম, জামানত ও অগ্রিম পরিশোধ", AccountClass.Asset),
        [FixedAssetAccount] = new(FixedAssetAccount, "Property, Plant & Equipment (cost)", "স্থায়ী সম্পদ (ক্রয়মূল্য)", AccountClass.Asset),
        [AccumulatedDepreciationAccount] = new(AccumulatedDepreciationAccount, "Accumulated Depreciation", "পুঞ্জীভূত অবচয়", AccountClass.Asset),
        [ProvisionsAccount] = new(ProvisionsAccount, "Provisions & Accrued Expenses", "সঞ্চিতি ও বকেয়া খরচ", AccountClass.Liability),
        [OtherLiabilitiesAccount] = new(OtherLiabilitiesAccount, "Other Liabilities", "অন্যান্য দায়", AccountClass.Liability),
        [DepreciationAccount] = new(DepreciationAccount, "Depreciation", "অবচয়", AccountClass.Expense),
    };

    /// <summary>Balance-sheet lines an opening entry may set (fixed assets come from the asset register).</summary>
    public static readonly string[] OpeningEntryAccounts =
    [
        CashAccount, ReceivableAccount, AdvancesDepositsAccount, InventoryAccount, LivestockAccount,
        LoanPayableAccount, PayableAccount, ProvisionsAccount, OtherLiabilitiesAccount,
        InvestorCapitalAccount, OwnerCapitalAccount, RetainedEarningsAccount,
    ];

    /// <summary>Balance-sheet lines a year-end adjustment may move: prepayments/accrued income and provisions/accruals.</summary>
    public static readonly string[] AdjustmentAccounts =
    [
        AdvancesDepositsAccount, ProvisionsAccount, OtherLiabilitiesAccount,
    ];

    /// <summary>Profit &amp; loss accounts an adjustment may post its other side to.</summary>
    public static IReadOnlyList<LedgerAccount> ProfitAndLossAccounts { get; } =
        Heads.Values
            .Where(h => h.Class is AccountClass.Revenue or AccountClass.Expense)
            .Select(h => new LedgerAccount(h.Code, h.Name, h.NameBn, h.Class))
            .Append(new LedgerAccount(DepreciationAccount, "Depreciation", "অবচয়", AccountClass.Expense))
            .DistinctBy(a => a.Code)
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .ToList();

    /// <summary>True when an account's normal balance is a debit (assets, expenses).</summary>
    public static bool IsDebitNature(string accountCode) =>
        Account(accountCode).Class is AccountClass.Asset or AccountClass.Expense;

    /// <summary>P&amp;L revenue categories — usable in EF queries.</summary>
    public static readonly TransactionCategory[] RevenueCategories =
        Heads.Where(h => h.Value.Class == AccountClass.Revenue).Select(h => h.Key).ToArray();

    /// <summary>P&amp;L expense categories — usable in EF queries.</summary>
    public static readonly TransactionCategory[] ExpenseCategories =
        Heads.Where(h => h.Value.Class == AccountClass.Expense).Select(h => h.Key).ToArray();

    /// <summary>Categories a person may record by hand on the Income form.</summary>
    public static readonly TransactionCategory[] ManualIncomeCategories =
    [
        TransactionCategory.MilkSale,
        TransactionCategory.ByproductSale,
        TransactionCategory.OtherIncome,
        TransactionCategory.OwnerCapital,
    ];

    /// <summary>
    /// Categories a person may record by hand on the Expense form. Everything else is posted by
    /// the module that owns the event (buy stock in Inventory, buy/sell animals in Livestock,
    /// loans and investors in Finance) so the asset, stock and register stay in step with the books.
    /// </summary>
    public static readonly TransactionCategory[] ManualExpenseCategories =
    [
        TransactionCategory.FeedCost,
        TransactionCategory.VeterinaryCost,
        TransactionCategory.MedicineCost,
        TransactionCategory.LaborCost,
        TransactionCategory.Utilities,
        TransactionCategory.Transport,
        TransactionCategory.MiscellaneousExpense,
        TransactionCategory.InterestExpense,
        TransactionCategory.OwnerDrawing,
    ];

    /// <summary>Purchases that may be made on credit (raise a supplier bill).</summary>
    public static readonly TransactionCategory[] CreditPurchaseCategories =
    [
        TransactionCategory.InventoryPurchase,
        TransactionCategory.AnimalPurchase,
        TransactionCategory.FixedAssetPurchase,
        TransactionCategory.FeedCost,
        TransactionCategory.VeterinaryCost,
        TransactionCategory.MedicineCost,
        TransactionCategory.LaborCost,
        TransactionCategory.Utilities,
        TransactionCategory.Transport,
        TransactionCategory.MiscellaneousExpense,
    ];

    /// <summary>Sales that may be made on credit (raise a customer invoice).</summary>
    public static readonly TransactionCategory[] CreditSaleCategories =
    [
        TransactionCategory.AnimalSale,
        TransactionCategory.MilkSale,
        TransactionCategory.ByproductSale,
        TransactionCategory.OtherIncome,
    ];

    /// <summary>The kind of credit bill a posting can carry, or null when it cannot be on credit.</summary>
    public static TradeBillKind? CreditKind(FinancialTransaction t)
    {
        ArgumentNullException.ThrowIfNull(t);
        if (t.Type == TransactionType.Expense && CreditPurchaseCategories.Contains(t.Category))
            return TradeBillKind.Payable;
        if (t.Type == TransactionType.Income && CreditSaleCategories.Contains(t.Category))
            return TradeBillKind.Receivable;
        return null;
    }

    public static AccountHead For(TransactionCategory category) =>
        Heads.TryGetValue(category, out var head)
            ? head
            : new AccountHead("5999", category.ToString(), category.ToString(), AccountClass.Expense, Operating, TransactionType.Expense, CostOverhead);

    public static LedgerAccount Account(string code) =>
        LedgerAccounts.TryGetValue(code, out var account)
            ? account
            : Heads.Values.Where(h => h.Code == code).Select(h => new LedgerAccount(h.Code, h.Name, h.NameBn, h.Class)).FirstOrDefault()
              ?? new LedgerAccount(code, code, code, AccountClass.Expense);

    /// <summary>
    /// The category name a profit &amp; loss account is reported under ("FeedCost", "AnimalSale"…);
    /// accounts no category posts to directly are named after themselves ("Depreciation").
    /// </summary>
    public static string CategoryKey(string accountCode) =>
        Heads.Where(h => h.Value.Code == accountCode).Select(h => h.Key.ToString()).FirstOrDefault()
        ?? (accountCode == DepreciationAccount ? "Depreciation" : Account(accountCode).Name);

    public static string SettlementCode(SettlementAccount settlement) => settlement switch
    {
        SettlementAccount.Inventory => InventoryAccount,
        SettlementAccount.Livestock => LivestockAccount,
        SettlementAccount.Payable => PayableAccount,
        SettlementAccount.Receivable => ReceivableAccount,
        SettlementAccount.FixedAsset => FixedAssetAccount,
        _ => CashAccount,
    };

    public static bool IsManualAllowed(TransactionType type, TransactionCategory category) =>
        type == TransactionType.Income
            ? ManualIncomeCategories.Contains(category)
            : ManualExpenseCategories.Contains(category);

    /// <summary>
    /// Rejects a hand-entered posting that belongs to another module, with directions to the right place.
    /// A manual "Stock Purchase", for instance, would put value in the Inventory account with no stock
    /// behind it, and a manual "Animal Sale" would earn revenue for an animal still in the herd.
    /// </summary>
    public static void EnsureManualAllowed(TransactionType type, TransactionCategory category)
    {
        if (IsManualAllowed(type, category))
            return;

        var where = category switch
        {
            TransactionCategory.InventoryPurchase or TransactionCategory.InventoryPurchaseReturn
                or TransactionCategory.InventoryWriteOff or TransactionCategory.ConsumableExpense =>
                "Record stock purchases, returns, usage and write-offs in Inventory (Stock In / Purchase Orders / Write-off).",
            TransactionCategory.AnimalPurchase or TransactionCategory.AnimalSale
                or TransactionCategory.CostOfAnimalsSold or TransactionCategory.LivestockLoss =>
                "Animal purchases, sales and deaths are booked from Livestock (register, sell or record death of the animal).",
            TransactionCategory.LoanDisbursement or TransactionCategory.LoanRepayment =>
                "Loans and repayments are booked from Finance > Loans so principal and interest stay correct.",
            TransactionCategory.InvestorCapital or TransactionCategory.InvestorWithdrawal or TransactionCategory.ProfitDistribution =>
                "Investor money is booked from Finance > Investors / Share Market.",
            TransactionCategory.SupplierPayment or TransactionCategory.CustomerReceipt or TransactionCategory.BadDebt =>
                "Payments against credit bills are recorded in Finance > Payables & Receivables, so the bill and the ledger stay in step.",
            TransactionCategory.FixedAssetPurchase or TransactionCategory.AssetDisposal =>
                "Sheds, machinery, vehicles and land are bought and disposed of in Finance > Fixed Assets, so the register and depreciation stay in step.",
            _ => $"'{category}' cannot be recorded as {(type == TransactionType.Income ? "income" : "an expense")}.",
        };

        throw new InvalidOperationException(where);
    }

    /// <summary>
    /// Automated postings mirror a document in another module; editing them here would let the books
    /// and that module disagree. Correct the source document instead and the posting follows.
    /// </summary>
    public static void EnsureEditable(FinancialTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.IsAutomated)
        {
            throw new InvalidOperationException(
                $"This entry was posted automatically by {transaction.SourceModule ?? "the system"} and cannot be changed here. " +
                "Correct the original record (stock entry, feeding entry, animal, loan, investor…) and the ledger updates itself.");
        }
    }

    // ── Classification helpers ──────────────────────────────────────────────

    /// <summary>True when the posting is revenue or expense — i.e. it belongs in profit &amp; loss.</summary>
    public static bool IsProfitAndLoss(FinancialTransaction t) =>
        For(t.Category).Class is AccountClass.Revenue or AccountClass.Expense;

    /// <summary>Kept for the cash-book reports: operating = P&amp;L.</summary>
    public static bool IsOperating(FinancialTransaction t) => IsProfitAndLoss(t);

    public static bool IsRevenue(FinancialTransaction t) => For(t.Category).Class == AccountClass.Revenue;

    public static bool IsExpense(FinancialTransaction t) => For(t.Category).Class == AccountClass.Expense;

    /// <summary>
    /// Signed P&amp;L effect: revenue adds, expense subtracts, balance-sheet movements are zero.
    /// Income-type rows on an expense account (e.g. a refund) reduce the expense.
    /// </summary>
    public static decimal ProfitEffect(FinancialTransaction t)
    {
        var cls = For(t.Category).Class;
        if (cls is not (AccountClass.Revenue or AccountClass.Expense))
            return 0m;
        return t.Type == TransactionType.Income ? t.AmountBdt : -t.AmountBdt;
    }

    /// <summary>Signed cash effect: zero for non-cash postings (stock consumed, animal sold at cost…).</summary>
    public static decimal CashEffect(FinancialTransaction t) =>
        t.Settlement != SettlementAccount.Cash ? 0m
        : t.Type == TransactionType.Income ? t.AmountBdt : -t.AmountBdt;

    /// <summary>Total P&amp;L revenue of a set of postings.</summary>
    public static decimal Revenue(IEnumerable<FinancialTransaction> postings) =>
        postings.Where(IsRevenue).Sum(ProfitEffect);

    /// <summary>Total P&amp;L expense of a set of postings (positive number).</summary>
    public static decimal Expenses(IEnumerable<FinancialTransaction> postings) =>
        -postings.Where(IsExpense).Sum(ProfitEffect);

    /// <summary>Net profit (revenue − expenses) of a set of postings.</summary>
    public static decimal NetProfit(IEnumerable<FinancialTransaction> postings) =>
        postings.Sum(ProfitEffect);

    /// <summary>Cash received (all cash-settled income-type postings).</summary>
    public static decimal CashIn(IEnumerable<FinancialTransaction> postings) =>
        postings.Where(t => t.Settlement == SettlementAccount.Cash && t.Type == TransactionType.Income).Sum(t => t.AmountBdt);

    /// <summary>Cash paid out (all cash-settled expense-type postings).</summary>
    public static decimal CashOut(IEnumerable<FinancialTransaction> postings) =>
        postings.Where(t => t.Settlement == SettlementAccount.Cash && t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);

    /// <summary>The double entry a posting represents.</summary>
    public static (string Debit, string Credit, decimal Amount) Journal(FinancialTransaction t)
    {
        var head = For(t.Category).Code;
        var settlement = SettlementCode(t.Settlement);
        return t.Type == TransactionType.Expense
            ? (head, settlement, t.AmountBdt)
            : (settlement, head, t.AmountBdt);
    }

    /// <summary>
    /// Account balances (debit-positive) from a set of postings: assets and expenses come out
    /// positive, liabilities, equity and revenue negative. Sums to zero by construction.
    /// </summary>
    public static Dictionary<string, decimal> Balances(IEnumerable<FinancialTransaction> postings)
    {
        var balances = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var t in postings)
        {
            var (debit, credit, amount) = Journal(t);
            balances[debit] = balances.GetValueOrDefault(debit) + amount;
            balances[credit] = balances.GetValueOrDefault(credit) - amount;
        }

        return balances;
    }

    /// <summary>
    /// A human voucher number. Automated postings store a source GUID as their reference, which
    /// means nothing on paper, so those fall back to a short code derived from the transaction id.
    /// </summary>
    public static string VoucherNo(FinancialTransaction t) =>
        !string.IsNullOrWhiteSpace(t.ReferenceId) && !Guid.TryParse(t.ReferenceId, out _)
            ? t.ReferenceId
            : $"TX-{t.Id.ToString("N")[..8].ToUpperInvariant()}";

    public static string Describe(FinancialTransaction t) =>
        !string.IsNullOrWhiteSpace(t.Description)
            ? t.Description
            : !string.IsNullOrWhiteSpace(t.Notes) ? t.Notes : For(t.Category).Name;

    /// <summary>Normalises a period to whole days: [from 00:00, to+1 00:00).</summary>
    public static (DateTime Start, DateTime EndExclusive) Window(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (start, end.AddDays(1));
    }
}
