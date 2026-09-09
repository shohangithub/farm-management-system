using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record InvestorDto(
    Guid Id,
    Guid FarmId,
    string Name,
    string? Email,
    string? Phone,
    string? NationalId,
    DateTime InvestmentDate,
    decimal TotalInvestedBdt,
    decimal TotalWithdrawnBdt,
    decimal CurrentCapitalBdt,
    decimal TotalProfitPaidBdt,
    decimal? AgreedProfitSharePercentage,
    decimal EffectiveSharePercentage,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<InvestorTransactionDto>? Transactions = null
);
