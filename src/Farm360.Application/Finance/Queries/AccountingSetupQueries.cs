using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

// ── Settings & options ─────────────────────────────────────────────────────

public record GetAccountingSettingsQuery(Guid FarmId) : IRequest<AccountingSettingsDto>;

public class GetAccountingSettingsQueryHandler(IFarmAccountingSettingsRepository settings)
    : IRequestHandler<GetAccountingSettingsQuery, AccountingSettingsDto>
{
    public async Task<AccountingSettingsDto> Handle(GetAccountingSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = await settings.GetByFarmIdAsync(request.FarmId, cancellationToken);
        return s is null
            ? new AccountingSettingsDto(request.FarmId, FiscalYear.DefaultStartMonth, null, false)
            : s.ToDto();
    }
}

public record GetAccountingSetupOptionsQuery : IRequest<AccountingSetupOptionsDto>;

public class GetAccountingSetupOptionsQueryHandler : IRequestHandler<GetAccountingSetupOptionsQuery, AccountingSetupOptionsDto>
{
    public Task<AccountingSetupOptionsDto> Handle(GetAccountingSetupOptionsQuery request, CancellationToken cancellationToken)
    {
        static AccountOptionDto Option(string code)
        {
            var a = AccountChart.Account(code);
            return new AccountOptionDto(a.Code, a.Name, a.NameBn, a.Class.ToString());
        }

        return Task.FromResult(new AccountingSetupOptionsDto(
            AccountChart.OpeningEntryAccounts.Select(Option).ToList(),
            AccountChart.AdjustmentAccounts.Select(Option).ToList(),
            AccountChart.ProfitAndLossAccounts.Select(a => new AccountOptionDto(a.Code, a.Name, a.NameBn, a.Class.ToString())).ToList()));
    }
}

// ── Opening balances & adjustments ─────────────────────────────────────────

public record GetBalanceSheetEntriesQuery(Guid FarmId) : IRequest<IReadOnlyList<BalanceSheetEntryDto>>;

public class GetBalanceSheetEntriesQueryHandler(IBalanceSheetEntryRepository entries)
    : IRequestHandler<GetBalanceSheetEntriesQuery, IReadOnlyList<BalanceSheetEntryDto>>
{
    public async Task<IReadOnlyList<BalanceSheetEntryDto>> Handle(GetBalanceSheetEntriesQuery request, CancellationToken cancellationToken)
    {
        var list = await entries.GetByFarmIdAsync(request.FarmId, cancellationToken);
        return list.Select(e => e.ToDto()).ToList();
    }
}

// ── Fixed assets ───────────────────────────────────────────────────────────

public record GetFixedAssetsQuery(Guid FarmId, DateTime? AsOfDate = null) : IRequest<IReadOnlyList<FixedAssetDto>>;

public class GetFixedAssetsQueryHandler(IFixedAssetRepository assets, IFarmAccountingSettingsRepository settings)
    : IRequestHandler<GetFixedAssetsQuery, IReadOnlyList<FixedAssetDto>>
{
    public async Task<IReadOnlyList<FixedAssetDto>> Handle(GetFixedAssetsQuery request, CancellationToken cancellationToken)
    {
        var fyMonth = (await settings.GetByFarmIdAsync(request.FarmId, cancellationToken))?.FiscalYearStartMonth
            ?? FiscalYear.DefaultStartMonth;
        var asOf = DateOnly.FromDateTime(request.AsOfDate ?? DateTime.UtcNow);
        var list = await assets.GetByFarmIdAsync(request.FarmId, cancellationToken);
        return list.Select(a => a.ToDto(asOf, fyMonth)).ToList();
    }
}

/// <param name="FromDate">Defaults to the start of the financial year containing <paramref name="ToDate"/>.</param>
public record GetFixedAssetScheduleQuery(Guid FarmId, DateTime? FromDate = null, DateTime? ToDate = null)
    : IRequest<FixedAssetScheduleDto>;

/// <summary>
/// The auditor's fixed-asset schedule for a period: cost brought forward, additions, disposals and
/// cost carried forward; depreciation brought forward, charge for the period, written back on
/// disposal and carried forward; and the written-down value.
/// </summary>
public class GetFixedAssetScheduleQueryHandler(IFixedAssetRepository assets, IFarmAccountingSettingsRepository settings)
    : IRequestHandler<GetFixedAssetScheduleQuery, FixedAssetScheduleDto>
{
    public async Task<FixedAssetScheduleDto> Handle(GetFixedAssetScheduleQuery request, CancellationToken cancellationToken)
    {
        var fyMonth = (await settings.GetByFarmIdAsync(request.FarmId, cancellationToken))?.FiscalYearStartMonth
            ?? FiscalYear.DefaultStartMonth;
        var to = DateOnly.FromDateTime(request.ToDate ?? DateTime.UtcNow);
        var from = request.FromDate is { } f ? DateOnly.FromDateTime(f) : FiscalYear.StartOf(to, fyMonth);
        if (from > to)
            throw new ArgumentException("The period start must be on or before its end.");

        var list = await assets.GetByFarmIdAsync(request.FarmId, cancellationToken);
        var lines = list
            .Select(a => FixedAssetSchedule.Line(a, from, to, fyMonth))
            .Where(l => l is not null)
            .Select(l => l!)
            .ToList();

        var totals = new FixedAssetScheduleLineDto(
            Guid.Empty, "", "Total", "", "", 0m, default, null,
            lines.Sum(l => l.CostOpeningBdt),
            lines.Sum(l => l.AdditionsBdt),
            lines.Sum(l => l.DisposalsBdt),
            lines.Sum(l => l.CostClosingBdt),
            lines.Sum(l => l.DepreciationOpeningBdt),
            lines.Sum(l => l.DepreciationChargeBdt),
            lines.Sum(l => l.DepreciationOnDisposalBdt),
            lines.Sum(l => l.DepreciationClosingBdt),
            lines.Sum(l => l.WrittenDownValueBdt));

        return new FixedAssetScheduleDto(
            request.FarmId,
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            lines,
            totals);
    }
}

public static class FixedAssetSchedule
{
    /// <summary>Depreciation charged up to the end of <paramref name="day"/> (0 before it was acquired).</summary>
    public static decimal AccumulatedAt(FixedAsset asset, DateOnly day, int fyMonth)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (day < asset.AcquisitionDate)
            return 0m;
        var until = asset.DisposalDate is { } d && d < day ? d : day;
        return asset.Cost - DepreciationCalculator.ValueAtEndOf(asset.Basis, until, fyMonth);
    }

    /// <summary>The asset's row for [from, to], or null when it was not held at any time in the period.</summary>
    public static FixedAssetScheduleLineDto? Line(FixedAsset asset, DateOnly from, DateOnly to, int fyMonth)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.AcquisitionDate > to || asset.DisposalDate is { } gone && gone < from)
            return null;

        var heldBefore = asset.AcquisitionDate < from;
        var disposedInPeriod = asset.DisposalDate is { } dd && dd <= to;

        var costOpening = heldBefore ? asset.Cost : 0m;
        var additions = heldBefore ? 0m : asset.Cost;
        var disposals = disposedInPeriod ? asset.Cost : 0m;

        var depOpening = heldBefore ? AccumulatedAt(asset, from.AddDays(-1), fyMonth) : 0m;
        var lastDay = disposedInPeriod ? asset.DisposalDate!.Value : to;
        var depAtEnd = AccumulatedAt(asset, lastDay, fyMonth);
        var charge = depAtEnd - depOpening;
        var depOnDisposal = disposedInPeriod ? depAtEnd : 0m;

        var costClosing = costOpening + additions - disposals;
        var depClosing = depOpening + charge - depOnDisposal;

        return new FixedAssetScheduleLineDto(
            asset.Id, asset.Code, asset.Name, asset.Category.ToString(), asset.Method.ToString(), asset.RatePercent,
            asset.AcquisitionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            asset.DisposalDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            costOpening, additions, disposals, costClosing,
            depOpening, charge, depOnDisposal, depClosing,
            costClosing - depClosing);
    }
}

internal static class AccountingSetupMappings
{
    public static AccountingSettingsDto ToDto(this FarmAccountingSettings s) =>
        new(s.FarmId, s.FiscalYearStartMonth, s.BooksStartDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), true);

    public static BalanceSheetEntryDto ToDto(this BalanceSheetEntry e) =>
        new(
            e.Id,
            e.Code,
            e.EntryType.ToString(),
            e.EntryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            e.AccountCode,
            AccountChart.Account(e.AccountCode).Name,
            e.Amount,
            e.ProfitAccountCode,
            e.ProfitAccountCode is null ? null : AccountChart.Account(e.ProfitAccountCode).Name,
            e.ReversalDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            e.Description,
            e.Notes);

    public static FixedAssetDto ToDto(this FixedAsset a, DateOnly asOf, int fyMonth)
    {
        var accumulated = FixedAssetSchedule.AccumulatedAt(a, asOf, fyMonth);
        return new FixedAssetDto(
            a.Id, a.Code, a.Name, a.Category.ToString(), a.Acquisition.ToString(),
            a.AcquisitionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            a.Cost, a.Method.ToString(), a.RatePercent, a.UsefulLifeYears, a.ResidualValue,
            a.DisposalDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            a.DisposalProceeds, a.Location, a.Notes,
            accumulated,
            a.IsDisposed && a.DisposalDate <= asOf ? 0m : a.Cost - accumulated);
    }
}
