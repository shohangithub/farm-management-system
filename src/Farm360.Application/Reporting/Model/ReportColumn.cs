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
    ReportAggregate Aggregate);

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
        ReportAggregate aggregate)
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

    public ReportColumn ToDescriptor() =>
        new(Field, Header, Type, Align, Decimals, Width, IsRelativeWidth, Aggregate);

    // ── Factories ───────────────────────────────────────────────────────────
    // Defaults encode the house style: text left, numbers right with fixed decimals.

    public static ReportColumn<TRow> Text(
        string field, LocalizedText header, Func<TRow, object?> value,
        float width = 2f, bool relative = true, ReportAlign align = ReportAlign.Left) =>
        new(field, header, ReportColumnType.Text, value, align, 0, width, relative, ReportAggregate.None);

    public static ReportColumn<TRow> Date(
        string field, LocalizedText header, Func<TRow, object?> value, float widthMm = 22f) =>
        new(field, header, ReportColumnType.Date, value, ReportAlign.Left, 0, widthMm, false, ReportAggregate.None);

    public static ReportColumn<TRow> DateTime(
        string field, LocalizedText header, Func<TRow, object?> value, float widthMm = 32f) =>
        new(field, header, ReportColumnType.DateTime, value, ReportAlign.Left, 0, widthMm, false, ReportAggregate.None);

    public static ReportColumn<TRow> WholeNumber(
        string field, LocalizedText header, Func<TRow, object?> value,
        float widthMm = 16f, ReportAggregate aggregate = ReportAggregate.None) =>
        new(field, header, ReportColumnType.WholeNumber, value, ReportAlign.Right, 0, widthMm, false, aggregate);

    public static ReportColumn<TRow> Number(
        string field, LocalizedText header, Func<TRow, object?> value,
        int decimals = 2, float widthMm = 20f, ReportAggregate aggregate = ReportAggregate.None) =>
        new(field, header, ReportColumnType.Number, value, ReportAlign.Right, decimals, widthMm, false, aggregate);

    public static ReportColumn<TRow> Money(
        string field, LocalizedText header, Func<TRow, object?> value,
        int decimals = 2, float widthMm = 24f, ReportAggregate aggregate = ReportAggregate.Sum) =>
        new(field, header, ReportColumnType.Money, value, ReportAlign.Right, decimals, widthMm, false, aggregate);

    public static ReportColumn<TRow> Percent(
        string field, LocalizedText header, Func<TRow, object?> value,
        int decimals = 1, float widthMm = 16f) =>
        new(field, header, ReportColumnType.Percent, value, ReportAlign.Right, decimals, widthMm, false, ReportAggregate.None);

    public static ReportColumn<TRow> Boolean(
        string field, LocalizedText header, Func<TRow, object?> value, float widthMm = 14f) =>
        new(field, header, ReportColumnType.Boolean, value, ReportAlign.Center, 0, widthMm, false, ReportAggregate.None);
}
