namespace Farm360.Domain.Finance.Enums;

/// <summary>
/// The other side of a ledger posting — what the money (or value) came from or went to.
/// </summary>
/// <remarks>
/// Together with the category this makes every single-row posting a complete double entry:
/// an Expense-type row debits its category account and credits the settlement account; an
/// Income-type row debits the settlement account and credits its category account.
/// Only <see cref="Cash"/> postings move the cash book. Consuming feed from stock, for example,
/// is Expense/FeedCost settled against <see cref="Inventory"/>: a real cost, but no cash moves —
/// the cash left when the stock was bought.
/// </remarks>
public enum SettlementAccount
{
    Cash = 0,
    Inventory = 1,
    Livestock = 2,

    /// <summary>Bought on credit — owed to the supplier until a supplier payment settles it.</summary>
    Payable = 3,

    /// <summary>Sold on credit — owed by the customer until a customer receipt settles it.</summary>
    Receivable = 4
}
