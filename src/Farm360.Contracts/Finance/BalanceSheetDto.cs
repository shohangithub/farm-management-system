using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record BalanceSheetLineDto(
    string LineCode,
    string LineName,
    decimal AmountBdt,
    string? Notes = null
)
{
    /// <summary>Sub-heading within the section: "Non-current", "Current", "Capital", "Reserves".</summary>
    public string? Group { get; init; }

    public string? NameBn { get; init; }

    /// <summary>The same line at the comparative date (null when no comparison was asked for).</summary>
    public decimal? PriorAmountBdt { get; init; }

    /// <summary>A deduction shown in brackets (accumulated depreciation, drawings, distributions).</summary>
    public bool IsDeduction { get; init; }
}

public record BalanceSheetSectionDto(
    string SectionName,
    decimal TotalBdt,
    IReadOnlyList<BalanceSheetLineDto> Lines
)
{
    public decimal? PriorTotalBdt { get; init; }
}

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

    /// <summary>Retained earnings brought forward from earlier financial years.</summary>
    public decimal RetainedEarningsBroughtForwardBdt { get; init; }

    /// <summary>Each asset/liability/equity account checked against its register (stock, herd, loans, investors).</summary>
    public IReadOnlyList<ReconciliationLineDto> Reconciliations { get; init; } = [];

    /// <summary>Comparative date (normally the previous financial year end).</summary>
    public DateTime? PriorDate { get; init; }

    public decimal? PriorTotalAssetsBdt { get; init; }
    public decimal? PriorTotalLiabilitiesAndEquityBdt { get; init; }
    public decimal? PriorDifferenceBdt { get; init; }

    /// <summary>Position date of the opening balances; null when the books run from the first posting.</summary>
    public DateTime? BooksStartDate { get; init; }

    public DateTime FiscalYearStart { get; init; }
    public int FiscalYearStartMonth { get; init; }

    /// <summary>False until the farm's accounting settings (books start / fiscal year) are saved.</summary>
    public bool IsConfigured { get; init; }

    /// <summary>Explains a Difference or a missing setup, for the banner above the statement.</summary>
    public string? Message { get; init; }
}
