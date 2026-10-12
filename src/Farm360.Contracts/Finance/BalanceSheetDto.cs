using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record BalanceSheetLineDto(
    string LineCode,
    string LineName,
    decimal AmountBdt,
    string? Notes = null
);

public record BalanceSheetSectionDto(
    string SectionName,
    decimal TotalBdt,
    IReadOnlyList<BalanceSheetLineDto> Lines
);

public record BalanceSheetDto(
    Guid FarmId,
    DateTime AsOfDate,
    BalanceSheetSectionDto Assets,
    BalanceSheetSectionDto Liabilities,
    BalanceSheetSectionDto Equity,
    decimal TotalAssetsBdt,
    decimal TotalLiabilitiesAndEquityBdt,
    bool IsBalanced,
    decimal DifferenceBdt,
    decimal NetWorkingCapitalBdt
)
{
    /// <summary>Net profit for the current financial year to date (part of retained earnings).</summary>
    public decimal CurrentYearProfitBdt { get; init; }

    /// <summary>Each asset/liability/equity account checked against its register (stock, herd, loans, investors).</summary>
    public IReadOnlyList<ReconciliationLineDto> Reconciliations { get; init; } = [];
}
