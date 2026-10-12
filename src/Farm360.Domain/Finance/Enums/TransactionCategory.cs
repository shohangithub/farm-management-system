namespace Farm360.Domain.Finance.Enums;

/// <summary>
/// The account a ledger posting belongs to. Which statement it lands on (P&amp;L, asset, liability,
/// equity) is decided by the chart of accounts (Application/Finance/Services/AccountChart), never
/// by the numeric range below — the ranges are historical and kept stable because values are stored.
/// </summary>
public enum TransactionCategory
{
    // ── Asset acquisitions & operating expenses ─────────────────────────────
    /// <summary>Livestock bought — capitalised as a Livestock asset, expensed on sale or death.</summary>
    AnimalPurchase = 1,
    FeedCost = 2,
    VeterinaryCost = 3,
    LaborCost = 4,
    Utilities = 5,
    Transport = 6,
    MiscellaneousExpense = 7,
    /// <summary>Stock bought — capitalised as an Inventory asset, expensed when consumed.</summary>
    InventoryPurchase = 8,
    MedicineCost = 9,
    ConsumableExpense = 10,
    /// <summary>Stock returned to a supplier — reduces the Inventory asset.</summary>
    InventoryPurchaseReturn = 11,
    /// <summary>Interest portion of a loan repayment.</summary>
    InterestExpense = 12,
    /// <summary>Stock damaged, expired or lost — written off from Inventory.</summary>
    InventoryWriteOff = 13,
    /// <summary>Book cost of animals sold or slaughtered — released from the Livestock asset.</summary>
    CostOfAnimalsSold = 14,
    /// <summary>Book cost of animals that died — written off from the Livestock asset.</summary>
    LivestockLoss = 15,
    /// <summary>Customer balances that will never be collected — written off from Accounts Receivable.</summary>
    BadDebt = 16,

    // ── Income Categories ───────────────────────────────────────────────────
    AnimalSale = 50,
    MilkSale = 51,
    ByproductSale = 52,
    OtherIncome = 53,

    // ── System / Loan Categories ────────────────────────────────────────────
    LoanDisbursement = 80,
    /// <summary>Principal portion of a loan repayment.</summary>
    LoanRepayment = 81,

    // ── Credit settlement (payables & receivables) ──────────────────────────
    /// <summary>Cash paid to a supplier against a bill — reduces Accounts Payable.</summary>
    SupplierPayment = 82,
    /// <summary>Cash collected from a customer against a credit sale — reduces Accounts Receivable.</summary>
    CustomerReceipt = 83,

    // ── Investor & Owner Categories ─────────────────────────────────────────
    InvestorCapital = 90,
    InvestorWithdrawal = 91,
    ProfitDistribution = 92,
    /// <summary>Owner's own capital brought in — cash, opening stock or animals.</summary>
    OwnerCapital = 93,
    /// <summary>Cash taken out by the owner for personal use.</summary>
    OwnerDrawing = 94
}
