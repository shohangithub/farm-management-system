using System;

namespace Farm360.Contracts.Finance;

public record SellSharesRequest(
    Guid InvestorId,
    int ShareCount,
    decimal? PricePerShareBdt = null,
    DateTime? TransactionDate = null,
    string? ReferenceId = null,
    string? Notes = null
);
