using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using MediatR;
using NSubstitute;

namespace Farm360.Application.UnitTests.Reporting;

internal sealed record StockRow(string Section, string Tag, string Unit, decimal Quantity, decimal Rate, decimal Value, decimal Share);

/// <summary>A small register used to exercise the platform's totalling rules.</summary>
internal sealed class StockTestReport : ReportDefinition<StockRow>
{
    private readonly IReadOnlyList<StockRow> _rows;

    public StockTestReport(IReadOnlyList<StockRow> rows, bool grouped = true)
    {
        _rows = rows;
        Grouped = grouped;
    }

    private bool Grouped { get; }

    public override string Key => "test.stock";

    public override LocalizedText Title => new("Stock", "মজুদ");

    public override ReportCategory Category => ReportCategory.Inventory;

    public override IReadOnlyList<ReportColumn<StockRow>> Columns =>
    [
        ReportColumn<StockRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.Tag),
        ReportColumn<StockRow>.Number("qty", new LocalizedText("Qty", "পরিমাণ"), r => r.Quantity, aggregate: ReportAggregate.Sum, unitOf: r => r.Unit),
        ReportColumn<StockRow>.Money("rate", new LocalizedText("Rate", "দর"), r => r.Rate),
        ReportColumn<StockRow>.Money("value", new LocalizedText("Value", "মূল্য"), r => r.Value, aggregate: ReportAggregate.Sum),
        ReportColumn<StockRow>.Percent("share", new LocalizedText("Share %", "অংশ %"), r => r.Share),
    ];

    public override ReportGroup<StockRow>? Group =>
        Grouped ? ReportGroup<StockRow>.ByLocalized(r => r.Section, r => new LocalizedText($"Section {r.Section}", $"বিভাগ {r.Section}")) : null;

    protected override Task<IReadOnlyList<StockRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken) =>
        Task.FromResult(_rows);
}

internal static class ReportingTestKit
{
    public static ReportContext Context(
        IReadOnlyDictionary<string, string?>? parameters = null,
        ReportLanguage language = ReportLanguage.En,
        bool bengaliNumerals = false,
        DateOnly? today = null,
        int maxRows = int.MaxValue) =>
        new(
            parameters ?? new Dictionary<string, string?>(),
            Substitute.For<ISender>(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tester",
            null,
            today ?? new DateOnly(2026, 11, 16),
            language,
            bengaliNumerals,
            maxRows: maxRows);

    public static Task<ReportDataSet> RunAsync(IReportDefinition definition, ReportContext? context = null) =>
        definition.ExecuteAsync(context ?? Context(), ReportMetaHeader.Empty, CancellationToken.None);
}
