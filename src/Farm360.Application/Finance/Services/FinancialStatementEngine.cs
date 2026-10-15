using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Interfaces;

namespace Farm360.Application.Finance.Services;

/// <summary>Where a ledger line came from.</summary>
public enum LedgerLineSource
{
    Posting,
    Opening,
    OpeningAsset,
    Adjustment,
    Reversal,
    Depreciation,
    Disposal,
}

/// <summary>One side of a double entry. <paramref name="Amount"/> is debit-positive (credit negative).</summary>
public sealed record LedgerLine(DateOnly Date, string Account, decimal Amount, LedgerLineSource Source);

/// <summary>A farm's books up to a date, as dated ledger lines every statement reads from.</summary>
public sealed class FarmLedger
{
    public FarmLedger(
        Guid farmId,
        int fiscalYearStartMonth,
        DateOnly? booksStartDate,
        IReadOnlyList<LedgerLine> lines,
        IReadOnlyList<FinancialTransaction> allPostings)
    {
        FarmId = farmId;
        FiscalYearStartMonth = fiscalYearStartMonth;
        BooksStartDate = booksStartDate;
        Lines = lines;
        AllPostings = allPostings;
    }

    public Guid FarmId { get; }
    public int FiscalYearStartMonth { get; }

    /// <summary>True once the farm's accounting settings have been saved.</summary>
    public bool IsConfigured { get; init; }

    /// <summary>Position date of the opening entries; postings on or before it are excluded.</summary>
    public DateOnly? BooksStartDate { get; }

    public IReadOnlyList<LedgerLine> Lines { get; }

    /// <summary>Every posting of the farm, unfiltered — for the register reconciliations.</summary>
    public IReadOnlyList<FinancialTransaction> AllPostings { get; }

    /// <summary>Debit-positive balance of every account at the end of <paramref name="asOf"/>.</summary>
    public Dictionary<string, decimal> BalancesAt(DateOnly asOf) =>
        Sum(Lines.Where(l => l.Date <= asOf));

    /// <summary>Debit-positive movement of every account in [from, to].</summary>
    public Dictionary<string, decimal> MovementsBetween(DateOnly from, DateOnly to) =>
        Sum(Lines.Where(l => l.Date >= from && l.Date <= to));

    /// <summary>Net profit (revenue − expenses) earned in [from, to].</summary>
    public decimal ProfitBetween(DateOnly from, DateOnly to) =>
        -Lines.Where(l => l.Date >= from && l.Date <= to && IsProfitAndLoss(l.Account)).Sum(l => l.Amount);

    /// <summary>First day the statements calculate from: the day after the books start, or the beginning.</summary>
    public DateOnly FirstCalculatedDay => BooksStartDate?.AddDays(1) ?? DateOnly.MinValue;

    public static bool IsProfitAndLoss(string account) =>
        AccountChart.Account(account).Class is AccountClass.Revenue or AccountClass.Expense;

    private static Dictionary<string, decimal> Sum(IEnumerable<LedgerLine> lines)
    {
        var balances = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            balances[line.Account] = balances.GetValueOrDefault(line.Account) + line.Amount;
        }

        return balances;
    }
}

public interface IFinancialStatementEngine
{
    /// <summary>Loads a farm's books with every line dated on or before <paramref name="upTo"/>.</summary>
    Task<FarmLedger> LoadAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default);
}

/// <summary>
/// The single source every financial statement is built from — so the balance sheet, income
/// statement and trial balance can never disagree.
/// </summary>
/// <remarks>
/// Lines come from: ledger postings after the books-start date; opening entries (one-sided, so an
/// incomplete opening position shows as a Difference rather than being plugged); year-end
/// adjustments and their automatic reversals (two-sided); opening fixed-asset values; depreciation
/// (Dr 5400 / Cr 1590, one line per asset per month, fiscal-year aware); and disposals
/// (net book value derecognised against 5410, where the sale proceeds are posted).
/// </remarks>
public sealed class FinancialStatementEngine : IFinancialStatementEngine
{
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IFarmAccountingSettingsRepository? _settings;
    private readonly IBalanceSheetEntryRepository? _entries;
    private readonly IFixedAssetRepository? _assets;

    public FinancialStatementEngine(
        IFinancialTransactionRepository transactions,
        IFarmAccountingSettingsRepository? settings = null,
        IBalanceSheetEntryRepository? entries = null,
        IFixedAssetRepository? assets = null)
    {
        _transactions = transactions;
        _settings = settings;
        _entries = entries;
        _assets = assets;
    }

    public async Task<FarmLedger> LoadAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default)
    {
        var settings = _settings is null ? null : await _settings.GetByFarmIdAsync(farmId, cancellationToken);
        var fyStartMonth = settings?.FiscalYearStartMonth ?? FiscalYear.DefaultStartMonth;
        var booksStart = settings?.BooksStartDate;

        var all = await _transactions.GetAllByFarmIdAsync(farmId, cancellationToken);
        var lines = new List<LedgerLine>();

        // ── Postings after the books start ─────────────────────────────────
        foreach (var t in all)
        {
            var date = DateOnly.FromDateTime(t.TransactionDate);
            if (date > upTo || (booksStart is { } start && date <= start))
                continue;

            var (debit, credit, amount) = AccountChart.Journal(t);
            lines.Add(new LedgerLine(date, debit, amount, LedgerLineSource.Posting));
            lines.Add(new LedgerLine(date, credit, -amount, LedgerLineSource.Posting));
        }

        // ── Opening entries and adjustments ────────────────────────────────
        if (_entries is not null)
        {
            foreach (var entry in await _entries.GetByFarmIdAsync(farmId, cancellationToken))
                AddEntryLines(lines, entry, upTo);
        }

        // ── Fixed assets: opening value, depreciation, disposal ────────────
        if (_assets is not null)
        {
            foreach (var asset in await _assets.GetByFarmIdAsync(farmId, cancellationToken))
                AddAssetLines(lines, asset, upTo, fyStartMonth);
        }

        return new FarmLedger(farmId, fyStartMonth, booksStart, lines, all.ToList())
        {
            IsConfigured = settings is not null,
        };
    }

    internal static void AddEntryLines(List<LedgerLine> lines, BalanceSheetEntry entry, DateOnly upTo)
    {
        if (entry.EntryDate > upTo)
            return;

        // Positive increases the line in its natural direction: a debit for assets, a credit otherwise.
        var signed = AccountChart.IsDebitNature(entry.AccountCode) ? entry.Amount : -entry.Amount;

        if (entry.EntryType == BalanceSheetEntryType.Opening)
        {
            lines.Add(new LedgerLine(entry.EntryDate, entry.AccountCode, signed, LedgerLineSource.Opening));
            return;
        }

        var profitAccount = entry.ProfitAccountCode!;
        lines.Add(new LedgerLine(entry.EntryDate, entry.AccountCode, signed, LedgerLineSource.Adjustment));
        lines.Add(new LedgerLine(entry.EntryDate, profitAccount, -signed, LedgerLineSource.Adjustment));

        if (entry.ReversalDate is { } reversal && reversal <= upTo)
        {
            lines.Add(new LedgerLine(reversal, entry.AccountCode, -signed, LedgerLineSource.Reversal));
            lines.Add(new LedgerLine(reversal, profitAccount, signed, LedgerLineSource.Reversal));
        }
    }

    internal static void AddAssetLines(List<LedgerLine> lines, FixedAsset asset, DateOnly upTo, int fyStartMonth)
    {
        if (asset.AcquisitionDate > upTo)
            return;

        // Owned before the books start: its written-down value is part of the opening position.
        // (Bought or contributed later, its cost reaches the ledger through its own posting.)
        if (asset.Acquisition == FixedAssetAcquisition.Opening)
            lines.Add(new LedgerLine(asset.AcquisitionDate, AccountChart.FixedAssetAccount, asset.Cost, LedgerLineSource.OpeningAsset));

        var basis = asset.Basis;
        var lastDay = asset.DisposalDate is { } disposed && disposed < upTo ? disposed : upTo;

        // One depreciation line per month (and one for the last part month), so any period —
        // fiscal year, month, retained-earnings window — is just a date filter.
        var previous = basis.Value;
        var segmentStart = basis.FromDay;
        while (segmentStart <= lastDay)
        {
            var monthEnd = new DateOnly(segmentStart.Year, segmentStart.Month, 1).AddMonths(1).AddDays(-1);
            var segmentEnd = monthEnd < lastDay ? monthEnd : lastDay;
            var value = DepreciationCalculator.ValueAtEndOf(basis, segmentEnd, fyStartMonth);
            var charge = previous - value;
            if (charge != 0m)
            {
                lines.Add(new LedgerLine(segmentEnd, AccountChart.DepreciationAccount, charge, LedgerLineSource.Depreciation));
                lines.Add(new LedgerLine(segmentEnd, AccountChart.AccumulatedDepreciationAccount, -charge, LedgerLineSource.Depreciation));
            }

            previous = value;
            segmentStart = segmentEnd.AddDays(1);
        }

        if (asset.DisposalDate is { } disposal && disposal <= upTo)
        {
            // Remove the asset at cost and its accumulated depreciation; the net book value
            // left over is charged to 5410, where the sale proceeds were posted.
            var netBookValue = DepreciationCalculator.ValueAtEndOf(basis, disposal, fyStartMonth);
            var accumulated = asset.Cost - netBookValue;
            lines.Add(new LedgerLine(disposal, AccountChart.AccumulatedDepreciationAccount, accumulated, LedgerLineSource.Disposal));
            lines.Add(new LedgerLine(disposal, AccountChart.FixedAssetAccount, -asset.Cost, LedgerLineSource.Disposal));
            lines.Add(new LedgerLine(disposal, AccountChart.AssetDisposalAccount, netBookValue, LedgerLineSource.Disposal));
        }
    }
}
