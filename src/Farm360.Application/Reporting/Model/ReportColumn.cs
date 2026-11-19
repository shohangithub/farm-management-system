using System;

namespace Farm360.Application.Reporting.Model;

/// <summary>
/// Column metadata as the renderers see it — no row type, no delegates, trivially serializable.
/// Produced from <see cref="ReportColumn{TRow}"/> when a definition executes.
/// </summary>
public sealed record ReportColumn(
    string Field,
    LocalizedText Header,
    ReportColumnType Type,
    ReportAlign Align,
    int Decimals,
    float Width,
    bool IsRelativeWidth,
    ReportAggregate Aggregate,
    bool MergeRepeating = false);

/// <summary>
/// Column declaration inside a report definition. Carries a typed accessor so definitions stay
/// compile-time safe; the platform flattens it to <see cref="ReportColumn"/> plus a value array.
/// </summary>
/// <remarks>
/// Widths are millimetres when <see cref="IsRelativeWidth"/> is false, otherwise proportional
/// units shared out across the remaining page width — the same model QuestPDF's table uses.
/// </remarks>
public sealed class ReportColumn<TRow>
{
    private ReportColumn(
        string field,
        LocalizedText header,
        ReportColumnType type,
        Func<TRow, object?> value,
        ReportAlign align,
        int decimals,
        float width,
        bool isRelativeWidth,
        ReportAggregate aggregate,
        bool mergeRepeating = false,
        Func<TRow, string?>? unitOf = null)
    {
        Field = field;
        Header = header;
        Type = type;
        Value = value;
        Align = align;
        Decimals = decimals;
        Width = width;
        IsRelativeWidth = isRelativeWidth;
        Aggregate = aggregate;
        MergeRepeating = mergeRepeating;
        UnitOf = unitOf;
    }

    public string Field { get; }
    public LocalizedText Header { get; }
    public ReportColumnType Type { get; }
    public Func<TRow, object?> Value { get; }
    public ReportAlign Align { get; }
    public int Decimals { get; }
    public float Width { get; }
    public bool IsRelativeWidth { get; }
    public ReportAggregate Aggregate { get; }
    public bool MergeRepeating { get; }

    /// <summary>
    /// The unit a quantity is measured in, per row. When set, a footer total is printed only if
    /// every row it covers shares one unit — 12 kg + 3 litres + 40 doses is not "55" of anything.
    /// </summary>
    public Func<TRow, string?>? UnitOf { get; }

    public ReportColumn ToDescriptor() =>
        new(Field, Header, Type, Align, Decimals, Width, IsRelativeWidth, Aggregate, MergeRepeating);

    // ── Factories ───────────────────────────────────────────────────────────
    // Defaults encode the house style: text left, numbers right with fixed decimals.

    public static ReportColumn<TRow> Text(
        string field, LocalizedText header, Func<TRow, object?> value,
        float width = 2f, bool relative = true, ReportAlign align = ReportAlign.Left, bool mergeRepeating = false) =>
        new(field, header, ReportColumnType.Text, value, align, 0, width, relative, ReportAggregate.None, mergeRepeating);

    public static ReportColumn<TRow> Date(
        string field, LocalizedText header, Func<TRow, object?> value, float widthMm = 22f, bool mergeRepeating = false) =>
        new(field, header, ReportColumnType.Date, value, ReportAlign.Left, 0, widthMm, false, ReportAggregate.None, mergeRepeating);

    public static ReportColumn<TRow> DateTime(
        string field, LocalizedText header, Func<TRow, object?> value, float widthMm = 32f) =>
        new(field, header, ReportColumnType.DateTime, value, ReportAlign.Left, 0, widthMm, false, ReportAggregate.None);

    public static ReportColumn<TRow> WholeNumber(
        string field, LocalizedText header, Func<TRow, object?> value,
        float widthMm = 16f, ReportAggregate aggregate = ReportAggregate.None) =>
        new(field, header, ReportColumnType.WholeNumber, value, ReportAlign.Right, 0, widthMm, false, aggregate);

    public static ReportColumn<TRow> Number(
        string field, LocalizedText header, Func<TRow, object?> value,
        int decimals = 2, float widthMm = 20f, ReportAggregate aggregate = ReportAggregate.None,
        Func<TRow, string?>? unitOf = null) =>
        new(field, header, ReportColumnType.Number, value, ReportAlign.Right, decimals, widthMm, false, aggregate, unitOf: unitOf);

    /// <summary>
    /// A money column. Not totalled unless the definition asks for it: a rate (BDT/kg), a cost per
    /// day or a price is money too, and summing those down a page prints a number that means
    /// nothing. Every total in a report is therefore a deliberate choice, never a default.
    /// </summary>
    public static ReportColumn<TRow> Money(
        string field, LocalizedText header, Func<TRow, object?> value,
        int decimals = 2, float widthMm = 24f, ReportAggregate aggregate = ReportAggregate.None) =>
        new(field, header, ReportColumnType.Money, value, ReportAlign.Right, decimals, widthMm, false, aggregate);

    public static ReportColumn<TRow> Percent(
        string field, LocalizedText header, Func<TRow, object?> value,
        int decimals = 1, float widthMm = 16f) =>
        new(field, header, ReportColumnType.Percent, value, ReportAlign.Right, decimals, widthMm, false, ReportAggregate.None);

    public static ReportColumn<TRow> Boolean(
        string field, LocalizedText header, Func<TRow, object?> value, float widthMm = 14f) =>
        new(field, header, ReportColumnType.Boolean, value, ReportAlign.Center, 0, widthMm, false, ReportAggregate.None);
}
