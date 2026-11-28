using System;

namespace Farm360.Contracts.Finance;

public record CreateInvestorRequest(
    string Name,
    decimal InitialInvestmentBdt,
    DateTime InvestmentDate,
    decimal? AgreedProfitSharePercentage = null,
    string? Email = null,
    string? Phone = null,
    string? NationalId = null,
    string? Notes = null,
    string? ReferenceId = null,
    /// <summary>The money account the first contribution goes into; null = the farm's default account.</summary>
    Guid? MoneyAccountId = null
);
