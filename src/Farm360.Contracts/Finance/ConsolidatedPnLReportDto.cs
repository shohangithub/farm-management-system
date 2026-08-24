using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record ConsolidatedPnLReportDto(
    int Year,
    int Month,
    decimal TotalIncomeBdt,
    decimal TotalExpenseBdt,
    decimal NetProfitBdt,
    IReadOnlyDictionary<Guid, FarmPnLSnapshotDto> FarmBreakdown
);

public record FarmPnLSnapshotDto(
    Guid FarmId,
    decimal TotalIncomeBdt,
    decimal TotalExpenseBdt,
    decimal NetProfitBdt
);
