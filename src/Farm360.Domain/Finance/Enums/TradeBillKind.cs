namespace Farm360.Domain.Finance.Enums;

/// <summary>Which side of a credit deal a bill is on.</summary>
public enum TradeBillKind
{
    /// <summary>The farm owes a supplier (bought on credit) — Accounts Payable.</summary>
    Payable = 0,

    /// <summary>A customer owes the farm (sold on credit) — Accounts Receivable.</summary>
    Receivable = 1
}

/// <summary>Where a bill stands. Derived from its amounts; never stored.</summary>
public enum TradeBillStatus
{
    Open,
    PartiallyPaid,
    Paid,
    /// <summary>Settled more than the bill now comes to (the source was reduced after payment).</summary>
    Overpaid,
    Cancelled
}
