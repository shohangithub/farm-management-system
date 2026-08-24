namespace Farm360.Domain.Finance.Enums;

public enum TransactionCategory
{
    // ── Expense Categories (PRD FR-FM-01 Chart of Accounts) ─────────────────
    AnimalPurchase = 1,
    FeedCost = 2,
    VeterinaryCost = 3,
    LaborCost = 4,
    Utilities = 5,
    Transport = 6,
    MiscellaneousExpense = 7,
    InventoryPurchase = 8,
    MedicineCost = 9,

    // ── Income Categories ───────────────────────────────────────────────────
    AnimalSale = 50,
    MilkSale = 51,
    ByproductSale = 52,
    OtherIncome = 53,

    // ── System / Loan Categories ────────────────────────────────────────────
    LoanDisbursement = 80,
    LoanRepayment = 81
}
