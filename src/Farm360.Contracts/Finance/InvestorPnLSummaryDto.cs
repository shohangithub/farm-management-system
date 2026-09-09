using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record InvestorShareDto(
    Guid InvestorId,
    string InvestorName,
    decimal InvestedCapitalBdt,
    decimal CapitalSharePercentage,
    decimal EffectiveSharePercentage,
    decimal AllocatedProfitBdt,
    decimal DistributedProfitBdt,
    decimal UndistributedProfitBdt,
    decimal NetEquityValueBdt,
    bool IsActive
);

public record InvestorPnLSummaryDto(
    Guid FarmId,
    DateTime? FromDate,
    DateTime? ToDate,
    decimal TotalFarmRevenueBdt,
    decimal TotalFarmExpensesBdt,
    decimal NetFarmProfitBdt,
    decimal CharityPercentage,
    decimal CharityAmountBdt,
    decimal DistributableProfitBdt,
    decimal TotalActiveCapitalBdt,
    decimal TotalDistributedProfitBdt,
    decimal TotalUndistributedProfitBdt,
    IReadOnlyList<InvestorShareDto> InvestorShares
);
