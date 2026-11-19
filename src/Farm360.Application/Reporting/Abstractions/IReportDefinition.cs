using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Abstractions;

/// <summary>
/// The Farm360 equivalent of an RDLC <c>.rdl</c> file: a code-first, compile-time-checked
/// declaration of one report's identity, parameters, page setup, columns and data source.
/// </summary>
/// <remarks>
/// Implementations are discovered by assembly scan at startup — adding a report means adding a
/// class, not editing a registry. Derive from <see cref="ReportDefinition{TRow}"/> rather than
/// implementing this directly; the base class does the flattening, grouping and totalling.
/// </remarks>
public interface IReportDefinition
{
    /// <summary>Stable, URL-safe identity, e.g. "livestock.animal-health". Never change it once shipped.</summary>
    string Key { get; }

    LocalizedText Title { get; }

    LocalizedText Description { get; }

    ReportCategory Category { get; }

    /// <summary>Permission code required on top of <c>reports.view</c>, or null for none.</summary>
    string? Permission { get; }

    IReadOnlyList<ReportParameter> Parameters { get; }

    PageSetup Page { get; }

    /// <summary>Column metadata without the typed accessors, for the catalog and the viewer.</summary>
    IReadOnlyList<ReportColumn> ColumnDescriptors { get; }

    /// <summary>
    /// One line describing what this run is about — the animal's tag and breed, the batch name.
    /// Printed under the title. Null when the report has no single subject (a herd register).
    /// </summary>
    Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken);

    Task<ReportDataSet> ExecuteAsync(ReportContext context, ReportMetaHeader meta, CancellationToken cancellationToken);
}

/// <summary>Single-level grouping. The platform emits a sub-total band whenever the key changes.</summary>
public sealed class ReportGroup<TRow>
{
    private ReportGroup(Func<TRow, string> keySelector, Func<TRow, ReportLanguage, string> labelSelector)
    {
        KeySelector = keySelector;
        LabelSelector = labelSelector;
    }

    public Func<TRow, string> KeySelector { get; }

    /// <summary>The printed band label, in the report's language.</summary>
    public Func<TRow, ReportLanguage, string> LabelSelector { get; }

    public static ReportGroup<TRow> By(Func<TRow, string> keySelector, Func<TRow, string>? labelSelector = null)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var label = labelSelector ?? keySelector;
        return new(keySelector, (row, _) => label(row));
    }

    /// <summary>A group whose band label is translated; use this for any fixed wording in the label.</summary>
    public static ReportGroup<TRow> ByLocalized(Func<TRow, string> keySelector, Func<TRow, LocalizedText> labelSelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(labelSelector);
        return new(keySelector, (row, language) => labelSelector(row).For(language));
    }
}

/// <summary>
/// Base class for every report. A concrete report supplies its metadata, its columns and a
/// <see cref="FetchAsync"/> that returns typed rows; everything downstream — cell formatting,
/// group sub-totals, grand totals, pagination, PDF, Excel, CSV — is handled here or by the renderers.
/// </summary>
public abstract class ReportDefinition<TRow> : IReportDefinition
{
    public abstract string Key { get; }

    public abstract LocalizedText Title { get; }

    public virtual LocalizedText Description => new(string.Empty, string.Empty);

    public abstract ReportCategory Category { get; }

    /// <summary>
    /// The permission needed to run this report: by default its module's view permission
    /// (finance reports need the finance permission). Override for a report that is more sensitive
    /// than its module.
    /// </summary>
    public virtual string? Permission => ReportPermissions.ForCategory(Category);

    public virtual IReadOnlyList<ReportParameter> Parameters => [];

    public virtual PageSetup Page => PageSetup.A4Portrait;

    public abstract IReadOnlyList<ReportColumn<TRow>> Columns { get; }

    public virtual ReportGroup<TRow>? Group => null;

    /// <summary>Set false for registers where a column sum is meaningless (e.g. a list of statuses).</summary>
    public virtual bool ShowGrandTotals => true;

    /// <summary>
    /// What the grand-total band is called. Statements whose bottom line is a result rather than a
    /// sum (a P&amp;L's net profit) say so here; null prints the generic "Grand total".
    /// </summary>
    public virtual LocalizedText? GrandTotalLabel => null;

    private IReadOnlyList<ReportColumn>? _columnDescriptors;

    public IReadOnlyList<ReportColumn> ColumnDescriptors =>
        _columnDescriptors ??= Columns.Select(c => c.ToDescriptor()).ToList();

    protected abstract Task<IReadOnlyList<TRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken);

    public virtual Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public async Task<ReportDataSet> ExecuteAsync(ReportContext context, ReportMetaHeader meta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var source = await FetchAsync(context, cancellationToken).ConfigureAwait(false);

        // Before formatting: building cells is the expensive part, so refuse here rather than after.
        context.EnsureWithinRowLimit(source.Count, Key);

        var columns = Columns;
        var descriptors = ColumnDescriptors;
        var fmt = new CellFormat(context.BengaliNumerals, context.Language);

        var rows = new List<ReportRow>(source.Count);
        var groupOrder = new List<string>();
        var groupBuckets = new Dictionary<string, List<TRow>>(StringComparer.Ordinal);
        var groupLabels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in source)
        {
            string? groupKey = null;
            if (Group is not null)
            {
                groupKey = Group.KeySelector(item);
                if (!groupBuckets.TryGetValue(groupKey, out var bucket))
                {
                    bucket = [];
                    groupBuckets[groupKey] = bucket;
                    groupLabels[groupKey] = Group.LabelSelector(item, context.Language);
                    groupOrder.Add(groupKey);
                }

                bucket.Add(item);
            }

            rows.Add(new ReportRow(BuildCells(item, columns, descriptors, fmt), groupKey));
        }

        var groupTotals = new List<ReportGroupTotals>(groupOrder.Count);
        foreach (var key in groupOrder)
        {
            var bucket = groupBuckets[key];
            groupTotals.Add(new ReportGroupTotals(
                key,
                groupLabels[key],
                bucket.Count,
                Aggregate(bucket, columns, descriptors, fmt)));
        }

        var grandTotals = ShowGrandTotals && HasAnyAggregate(columns)
            ? Aggregate(source, columns, descriptors, fmt)
            : [];

        // A grand-total band in which every cell came out blank (mixed units everywhere) is noise.
        var hasGrandTotals = grandTotals.Length > 0 && Array.Exists(grandTotals, c => c.V is not null);

        return new ReportDataSet(
            Key,
            Title.For(context.Language),
            Category,
            Page,
            meta,
            descriptors,
            rows,
            groupTotals,
            grandTotals,
            hasGrandTotals,
            source.Count,
            context.Language,
            context.BengaliNumerals,
            GrandTotalLabel?.For(context.Language));
    }

    private readonly record struct CellFormat(bool BengaliNumerals, ReportLanguage Language);

    private static bool HasAnyAggregate(IReadOnlyList<ReportColumn<TRow>> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Aggregate != ReportAggregate.None)
            {
                return true;
            }
        }

        return false;
    }

    private static ReportCell[] BuildCells(
        TRow item,
        IReadOnlyList<ReportColumn<TRow>> columns,
        IReadOnlyList<ReportColumn> descriptors,
        CellFormat fmt)
    {
        var cells = new ReportCell[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var value = columns[i].Value(item);
            cells[i] = new ReportCell(value, Format(value, descriptors[i], fmt));
        }

        return cells;
    }

    private static string Format(object? value, ReportColumn descriptor, CellFormat fmt) =>
        ReportValueFormatter.Format(value, descriptor.Type, descriptor.Decimals, fmt.BengaliNumerals, fmt.Language);

    /// <summary>
    /// Footer totals, under rules that keep a total honest: percentages are never added, and a
    /// quantity with a unit is only totalled when every row it covers shares that unit.
    /// </summary>
    private static ReportCell[] Aggregate(
        IReadOnlyCollection<TRow> items,
        IReadOnlyList<ReportColumn<TRow>> columns,
        IReadOnlyList<ReportColumn> descriptors,
        CellFormat fmt)
    {
        var cells = new ReportCell[columns.Count];

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (column.Aggregate == ReportAggregate.None)
            {
                cells[i] = new ReportCell(null, string.Empty);
                continue;
            }

            if (column.Aggregate == ReportAggregate.Count)
            {
                cells[i] = new ReportCell(items.Count, ReportValueFormatter.Format(items.Count, ReportColumnType.WholeNumber, 0, fmt.BengaliNumerals, fmt.Language));
                continue;
            }

            if (!CanAggregate(column, items))
            {
                cells[i] = new ReportCell(null, string.Empty);
                continue;
            }

            decimal? result = null;
            var count = 0;

            foreach (var item in items)
            {
                var raw = column.Value(item);
                if (raw is null)
                {
                    continue;
                }

                var value = ReportValueFormatter.ToDecimal(raw);
                count++;
                result = column.Aggregate switch
                {
                    ReportAggregate.Sum or ReportAggregate.Average => (result ?? 0m) + value,
                    ReportAggregate.Min => result is null || value < result ? value : result,
                    ReportAggregate.Max => result is null || value > result ? value : result,
                    _ => result,
                };
            }

            if (column.Aggregate == ReportAggregate.Average && count > 0 && result is not null)
            {
                result = result.Value / count;
            }

            cells[i] = result is null
                ? new ReportCell(null, string.Empty)
                : new ReportCell(result.Value, Format(result.Value, descriptors[i], fmt));
        }

        return cells;
    }

    /// <summary>The totalling rules for one column over a set of rows.</summary>
    public static bool CanAggregate(ReportColumn<TRow> column, IEnumerable<TRow> items)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(items);

        // A percentage summed is nonsense and averaged unweighted is misleading; Min/Max are fine.
        if (column.Type == ReportColumnType.Percent
            && column.Aggregate is ReportAggregate.Sum or ReportAggregate.Average)
        {
            return false;
        }

        if (column.Aggregate is ReportAggregate.Sum or ReportAggregate.Average
            && !ReportValueFormatter.IsAdditive(column.Type))
        {
            return false;
        }

        if (column.UnitOf is null)
        {
            return true;
        }

        string? unit = null;
        var first = true;
        foreach (var item in items)
        {
            var u = column.UnitOf(item)?.Trim();
            if (first)
            {
                unit = u;
                first = false;
            }
            else if (!string.Equals(unit, u, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// The permission each report category requires. Codes mirror
/// <c>Farm360.Persistence.Seed.PermissionConstants</c>, which the Application layer cannot reference.
/// </summary>
public static class ReportPermissions
{
    public const string LivestockView = "animals.view";
    public const string FeedingView = "feeding.view";
    public const string HealthView = "health.view";
    public const string InventoryView = "inventory.view";
    public const string FinanceView = "finance.view";

    public static string? ForCategory(ReportCategory category) => category switch
    {
        ReportCategory.Livestock => LivestockView,
        ReportCategory.Feeding => FeedingView,
        ReportCategory.Health => HealthView,
        ReportCategory.Inventory => InventoryView,
        ReportCategory.Finance => FinanceView,
        _ => null,
    };
}
