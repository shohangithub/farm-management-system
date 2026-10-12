using System;

namespace Farm360.Contracts.Finance;

public record FinancialTransactionDto(
    Guid Id,
    Guid FarmId,
    string Type,
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string ReferenceId,
    string Notes,
    Guid? AnimalId,
    Guid? BatchId,
    Guid? ShedId,
    DateTime CreatedAtUtc,
    bool IsAutomated = false,
    string? SourceModule = null
)
{
    /// <summary>Cash, Inventory or Livestock — what the posting was settled against. Only Cash moves the cash book.</summary>
    public string Settlement { get; init; } = "Cash";

    /// <summary>False for non-cash postings (stock consumed, cost of animals sold, mortality).</summary>
    public bool AffectsCash { get; init; } = true;

    /// <summary>Chart-of-accounts code of the head, e.g. 5020.</summary>
    public string AccountCode { get; init; } = string.Empty;

    /// <summary>Presentation group: Operating, Asset, Capital or Financing.</summary>
    public string AccountGroup { get; init; } = string.Empty;

    /// <summary>Asset, Liability, Equity, Revenue or Expense.</summary>
    public string AccountClass { get; init; } = string.Empty;
}
