using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

// ── Income statement ───────────────────────────────────────────────────────

public record IncomeStatementLineDto(
    string AccountCode,
    string AccountName,
    string AccountNameBn,
    decimal AmountBdt,
    decimal? PriorAmountBdt);

/// <param name="Key">revenue, cost-of-sales, operating-expenses or finance-costs.</param>
public record IncomeStatementSectionDto(
    string Key,
    string Name,
    string NameBn,
    IReadOnlyList<IncomeStatementLineDto> Lines,
    decimal TotalBdt,
    decimal? PriorTotalBdt);

public record IncomeStatementTotalsDto(
    decimal RevenueBdt,
    decimal CostOfSalesBdt,
    decimal GrossProfitBdt,
    decimal OperatingExpensesBdt,
    decimal OperatingProfitBdt,
    decimal FinanceCostsBdt,
    decimal NetProfitBdt)
{
    /// <summary>Gross profit as a % of revenue (0 when there is no revenue).</summary>
    public decimal GrossMarginPercent => RevenueBdt == 0 ? 0 : Math.Round(GrossProfitBdt / RevenueBdt * 100m, 2);

    public decimal NetMarginPercent => RevenueBdt == 0 ? 0 : Math.Round(NetProfitBdt / RevenueBdt * 100m, 2);
}

public record IncomeStatementDto(
    Guid FarmId,
    DateTime FromDate,
    DateTime ToDate,
    DateTime? PriorFromDate,
    DateTime? PriorToDate,
    IReadOnlyList<IncomeStatementSectionDto> Sections,
    IncomeStatementTotalsDto Totals,
    IncomeStatementTotalsDto? PriorTotals,
    int FiscalYearStartMonth,
    DateTime? BooksStartDate);

// ── Fixed asset schedule ───────────────────────────────────────────────────

public record FixedAssetScheduleLineDto(
    Guid AssetId,
    string Code,
    string Name,
    string Category,
    string Method,
    decimal RatePercent,
    DateTime AcquisitionDate,
    DateTime? DisposalDate,
    decimal CostOpeningBdt,
    decimal AdditionsBdt,
    decimal DisposalsBdt,
    decimal CostClosingBdt,
    decimal DepreciationOpeningBdt,
    decimal DepreciationChargeBdt,
    decimal DepreciationOnDisposalBdt,
    decimal DepreciationClosingBdt,
    decimal WrittenDownValueBdt);

public record FixedAssetScheduleDto(
    Guid FarmId,
    DateTime FromDate,
    DateTime ToDate,
    IReadOnlyList<FixedAssetScheduleLineDto> Lines,
    FixedAssetScheduleLineDto Totals);

// ── Accounting setup ───────────────────────────────────────────────────────

public record AccountingSettingsDto(
    Guid FarmId,
    int FiscalYearStartMonth,
    DateTime? BooksStartDate,
    bool IsConfigured);

public record AccountOptionDto(string Code, string Name, string NameBn, string AccountClass);

/// <summary>The accounts the setup forms may choose from.</summary>
public record AccountingSetupOptionsDto(
    IReadOnlyList<AccountOptionDto> OpeningAccounts,
    IReadOnlyList<AccountOptionDto> AdjustmentAccounts,
    IReadOnlyList<AccountOptionDto> ProfitAndLossAccounts);

public record BalanceSheetEntryDto(
    Guid Id,
    string Code,
    string EntryType,
    DateTime EntryDate,
    string AccountCode,
    string AccountName,
    decimal AmountBdt,
    string? ProfitAccountCode,
    string? ProfitAccountName,
    DateTime? ReversalDate,
    string Description,
    string? Notes);

public record SaveBalanceSheetEntryRequest(
    string EntryType,
    DateTime EntryDate,
    string AccountCode,
    decimal AmountBdt,
    string Description,
    string? ProfitAccountCode = null,
    DateTime? ReversalDate = null,
    string? Notes = null);

public record FixedAssetDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string Acquisition,
    DateTime AcquisitionDate,
    decimal CostBdt,
    string Method,
    decimal RatePercent,
    decimal UsefulLifeYears,
    decimal ResidualValueBdt,
    DateTime? DisposalDate,
    decimal DisposalProceedsBdt,
    string? Location,
    string? Notes,
    decimal AccumulatedDepreciationBdt,
    decimal WrittenDownValueBdt);

public record SaveFixedAssetRequest(
    string Name,
    string Category,
    string Acquisition,
    DateTime AcquisitionDate,
    decimal CostBdt,
    string Method,
    decimal RatePercent,
    decimal UsefulLifeYears,
    decimal ResidualValueBdt,
    string? Location = null,
    string? Notes = null,
    CreditTermsRequest? Credit = null);

public record DisposeFixedAssetRequest(DateTime DisposalDate, decimal ProceedsBdt);

public record SaveAccountingSettingsRequest(int FiscalYearStartMonth, DateTime? BooksStartDate);
