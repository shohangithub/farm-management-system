using System;

namespace Farm360.Contracts.Finance;

public record AnimalCostLedgerDto(
    Guid AnimalId,
    Guid FarmId,
    decimal AcquisitionCostBdt,
    decimal TotalFeedCostBdt,
    decimal TotalVetCostBdt,
    decimal TotalLaborCostBdt,
    decimal TotalOverheadBdt,
    decimal TotalCostBdt,
    decimal? SaleRevenueBdt,
    decimal? ProfitLossBdt
);
