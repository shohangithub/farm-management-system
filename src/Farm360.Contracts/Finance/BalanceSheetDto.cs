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
);
