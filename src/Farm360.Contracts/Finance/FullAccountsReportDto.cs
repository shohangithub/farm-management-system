using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record CashPositionDto(
    decimal OpeningBalanceBdt,
    decimal TotalInflowBdt,
    decimal TotalOutflowBdt,
    decimal NetCashFlowBdt,
    decimal ClosingBalanceBdt,
    int TransactionCount
);

public record OperatingStatementDto(
    IReadOnlyList<AccountHeadSummaryDto> RevenueLines,
    IReadOnlyList<AccountHeadSummaryDto> ExpenseLines,
    decimal TotalRevenueBdt,
    decimal TotalExpenseBdt,
    decimal NetOperatingProfitBdt,
    decimal ProfitMarginPercent
);

/// <summary>Operating cost rolled up into the buckets a farm owner actually thinks in.</summary>
public record CostGroupDto(
    string Key,
    string Name,
    string NameBn,
    decimal AmountBdt,
    decimal PercentOfTotal,
    int Count
);

public record CostAnalysisDto(
    IReadOnlyList<CostGroupDto> Groups,
    decimal TotalOperatingCostBdt,
    decimal LabourCostBdt,
    decimal FeedCostBdt,
    decimal HealthCostBdt,
    decimal CostOfAnimalsBdt,
    decimal OverheadCostBdt,
    decimal AutomatedPostingsBdt,
    decimal ManualPostingsBdt,
    decimal AverageDailyCostBdt
);

public record CapitalFlowDto(
    decimal InvestorCapitalInBdt,
    decimal InvestorWithdrawalBdt,
    decimal ProfitDistributedBdt,
    decimal LoanReceivedBdt,
    decimal LoanRepaidBdt,
    decimal NetCapitalFlowBdt
)
{
    public decimal OwnerCapitalInBdt { get; init; }
    public decimal OwnerDrawingBdt { get; init; }
    public decimal LoanInterestPaidBdt { get; init; }
}

/// <summary>
/// How the two non-cash assets moved in the period. Stock and animals are bought with cash but
/// only become costs when used, sold or lost — this section shows that bridge.
/// </summary>
public record AssetMovementDto(
    decimal InventoryOpeningBdt,
    decimal StockPurchasedBdt,
    decimal StockBroughtInBdt,
    decimal StockReturnedBdt,
    decimal StockConsumedBdt,
    decimal StockWrittenOffBdt,
    decimal InventoryClosingBdt,
    decimal LivestockOpeningBdt,
    decimal AnimalsPurchasedBdt,
    decimal AnimalsBroughtInBdt,
    decimal CostOfAnimalsSoldBdt,
    decimal MortalityLossBdt,
    decimal LivestockClosingBdt
);

public record InvestorAccountDto(
    Guid InvestorId,
    string Name,
    string? Phone,
    bool IsActive,
    decimal? AgreedProfitSharePercentage,
    decimal OpeningCapitalBdt,
    decimal ContributedInPeriodBdt,
    decimal WithdrawnInPeriodBdt,
    decimal ClosingCapitalBdt,
    decimal ProfitSharePercentage,
    decimal ProfitEntitledInPeriodBdt,
    decimal ProfitPaidInPeriodBdt,
    decimal ProfitPaidLifetimeBdt,
    decimal ProfitPayableBdt,
    int ShareCount,
    decimal OwnershipPercentage,
    decimal ShareMarketValueBdt
);

public record ProfitShareSummaryDto(
    decimal NetOperatingProfitBdt,
    decimal CharityPercentage,
    decimal CharityAmountBdt,
    decimal DistributableProfitBdt,
    decimal TotalEntitledBdt,
    decimal DistributedInPeriodBdt,
    decimal RetainedProfitBdt
);

public record ShareCapitalSummaryDto(
    bool IsConfigured,
    int TotalShares,
    decimal SharePriceBdt,
    int OwnerShareCount,
    int AllocatedShareCount,
    int AvailableShareCount,
    decimal TotalValuationBdt,
    decimal OwnerOwnershipPercentage,
    int SharesIssuedInPeriod,
    decimal ShareSalesAmountBdt,
    int SharesBoughtBackInPeriod,
    decimal BuybackAmountBdt,
    int SharesTransferredInPeriod,
    IReadOnlyList<ShareTransactionDto> Transactions
);

public record LoanAccountDto(
    Guid LoanId,
    string LenderName,
    DateTime DisbursementDate,
    decimal PrincipalAmountBdt,
    decimal InterestRatePercent,
    decimal TotalPayableBdt,
    decimal RepaidInPeriodBdt,
    decimal TotalRepaidBdt,
    decimal OutstandingBdt,
    decimal PrincipalRepaidBdt,
    decimal InterestPaidBdt,
    decimal OutstandingPrincipalBdt,
    bool DisbursedInPeriod,
    bool IsActive
);

public record LoanSummaryDto(
    decimal DisbursedInPeriodBdt,
    decimal RepaidInPeriodBdt,
    decimal InterestPaidInPeriodBdt,
    decimal TotalPrincipalBdt,
    decimal TotalOutstandingBdt,
    int ActiveLoanCount,
    IReadOnlyList<LoanAccountDto> Loans
);

/// <summary>One bucket of the period trend — a day for short ranges, a month for long ones.</summary>
public record PeriodTrendDto(
    string Label,
    DateTime StartDate,
    DateTime EndDate,
    decimal OperatingIncomeBdt,
    decimal OperatingExpenseBdt,
    decimal NetOperatingBdt,
    decimal CapitalInBdt,
    decimal CapitalOutBdt,
    decimal ClosingBalanceBdt
);

/// <summary>A cash-book line with its running balance, for the full ledger section.</summary>
public record LedgerEntryDto(
    Guid Id,
    DateTime TransactionDate,
    string VoucherNo,
    string Code,
    string HeadName,
    string Group,
    string Description,
    decimal InflowBdt,
    decimal OutflowBdt,
    decimal RunningBalanceBdt,
    bool IsAutomated,
    string? SourceModule
);

/// <summary>
/// Full accounts statement for a period: cash position, operating P&amp;L, cost analysis (labour, feed,
/// health…), investor capital, profit sharing, share capital, loans, trend and the complete ledger.
/// </summary>
public record FullAccountsReportDto(
    Guid FarmId,
    DateTime FromDate,
    DateTime ToDate,
    int Days,
    string TrendGranularity,
    CashPositionDto Cash,
    OperatingStatementDto Operating,
    CostAnalysisDto Costs,
    CapitalFlowDto Capital,
    AssetMovementDto Assets,
    IReadOnlyList<InvestorAccountDto> Investors,
    ProfitShareSummaryDto ProfitShare,
    ShareCapitalSummaryDto Shares,
    LoanSummaryDto Loans,
    IReadOnlyList<PeriodTrendDto> Trend,
    IReadOnlyList<LedgerEntryDto> Ledger
)
{
    /// <summary>Credit sales and purchases in the period, and what is still owed either way.</summary>
    public TradeCreditMovementDto? Credit { get; init; }
}
