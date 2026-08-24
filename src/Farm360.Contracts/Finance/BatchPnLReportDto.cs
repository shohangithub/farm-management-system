using System;

namespace Farm360.Contracts.Finance;

public record BatchPnLReportDto(
    Guid BatchId,
    Guid FarmId,
    decimal TotalIncomeBdt,
    decimal TotalCostBdt,
    decimal GrossProfitBdt,
    decimal ReturnOnInvestmentPercent,
    int TotalAnimals
);
