using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

/// <summary>
/// A partner's position. Capital = put in − taken out. Share = capital ÷ all partners' capital.
/// Profit allocated is split by capital-days, fiscal year by fiscal year; due = allocated − paid.
/// </summary>
public record PartnerSummaryDto(
    Guid Id,
    string Name,
    string? Phone,
    bool IsActive,
    DateTime JoinedOn,
    decimal DepositedBdt,
    decimal WithdrawnBdt,
    decimal CapitalBdt,
    decimal SharePercent,
    decimal ProfitAllocatedBdt,
    decimal ProfitPaidBdt,
    decimal ProfitDueBdt,
    decimal EquityBdt);

public record PartnersOverviewDto(
    decimal TotalCapitalBdt,
    decimal NetAssetsBdt,
    decimal ProfitAllocatedBdt,
    decimal ProfitPaidBdt,
    IReadOnlyList<PartnerSummaryDto> Partners);

/// <summary>One partner's share of a period's profit, by capital-days.</summary>
/// <param name="AverageCapitalBdt">Capital-days ÷ days in the period — the capital held on an average day.</param>
public record PartnerAllocationLineDto(
    Guid PartnerId,
    string Name,
    decimal CapitalDays,
    decimal AverageCapitalBdt,
    decimal SharePercent,
    decimal AllocatedBdt);

public record PartnerProfitAllocationDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int Days,
    decimal NetProfitBdt,
    decimal CharityPercent,
    decimal CharityBdt,
    decimal DistributableProfitBdt,
    IReadOnlyList<PartnerAllocationLineDto> Lines);

/// <summary>A line of a partner's capital ledger.</summary>
/// <param name="Type">CapitalContribution, CapitalWithdrawal, ProfitDistribution or ProfitAllocation.</param>
public record PartnerLedgerLineDto(
    Guid? Id,
    DateTime Date,
    string Type,
    decimal AmountBdt,
    decimal CapitalAfterBdt,
    string? MoneyAccountName,
    string? ReferenceId,
    string? Notes);

/// <summary>A line of the farm's net assets and the partner's part of it.</summary>
public record NetAssetLineDto(string Key, string Name, string NameBn, decimal FarmBdt, decimal PartnerBdt);

public record PartnerStatementDto(
    PartnerSummaryDto Partner,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningCapitalBdt,
    decimal ClosingCapitalBdt,
    IReadOnlyList<PartnerLedgerLineDto> Lines,
    PartnerProfitAllocationDto Allocation,
    IReadOnlyList<NetAssetLineDto> NetAssets,
    decimal NetAssetsBdt,
    decimal PartnerNetAssetsBdt);

public record PartnerPartDto(Guid PartnerId, string Name, decimal SharePercent, decimal AmountBdt);

/// <summary>A spend and each partner's pro-rata part of it (by capital on the day of the spend).</summary>
public record WhoseMoneyRowDto(
    Guid TransactionId,
    DateTime Date,
    string Description,
    string Category,
    string CategoryName,
    string CategoryNameBn,
    decimal AmountBdt,
    Guid? AnimalId,
    IReadOnlyList<PartnerPartDto> Parts);

public record PartnerRefDto(Guid Id, string Name);

public record WhoseMoneyDto(
    IReadOnlyList<PartnerRefDto> Partners,
    IReadOnlyList<WhoseMoneyRowDto> Rows,
    decimal TotalBdt,
    IReadOnlyList<PartnerPartDto> Totals);
