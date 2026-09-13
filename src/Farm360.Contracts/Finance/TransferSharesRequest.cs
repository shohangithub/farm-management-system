using System;

namespace Farm360.Contracts.Finance;

public record TransferSharesRequest(
    Guid FromInvestorId,
    Guid ToInvestorId,
    int ShareCount,
    decimal? PricePerShareBdt = null,
    DateTime? TransactionDate = null,
    string? ReferenceId = null,
    string? Notes = null
);
