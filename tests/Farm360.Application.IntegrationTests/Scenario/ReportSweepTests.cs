using System.Globalization;
using System.Text;
using System.Text.Json;
using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Registry;
using Farm360.Application.Reporting.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Farm360.Application.IntegrationTests.Scenario;

/// <summary>
/// Every report in the catalogue, run through the real reporting engine on the scenario farm, in
/// English and Bangla, to screen (JSON), PDF and Excel: none may throw, every summed column's grand
/// total must equal the sum of its rows, and no cell may show NaN, a negative zero or a raw enum name.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ReportSweepTests(SqlServerFixture fixture, ITestOutputHelper output)
{
    private static readonly HashSet<string> EnumNames = BuildEnumNames();

    /// <summary>Single-word enum names ("Sold", "Male", "Bank") that a Bangla report must translate.</summary>
    private static readonly HashSet<string> SingleWordEnumNames = new[]
        {
            typeof(Farm360.Domain.Livestock.Enums.AnimalStatus),
            typeof(Farm360.Domain.Livestock.Enums.AnimalSex),
            typeof(Farm360.Domain.Livestock.Enums.AcquisitionType),
            typeof(Farm360.Domain.Livestock.Enums.DisposalReason),
            typeof(Farm360.Domain.Finance.Enums.MoneyAccountKind),
            typeof(Farm360.Domain.Finance.Enums.TradeBillKind),
            typeof(Farm360.Domain.Finance.Enums.TransactionType),
            typeof(Farm360.Domain.Inventory.Enums.InventoryCategory),
        }
        .SelectMany(Enum.GetNames)
        .Where(n => !n.Skip(1).Any(char.IsUpper))
        .ToHashSet(StringComparer.Ordinal);

    [SqlServerFact]
    public async Task EveryReportRunsAndAddsUpInBothLanguagesAndEveryFormat()
    {
        await using var host = await ScenarioHost.CreateAsync(fixture.ConnectionString);
        var s = new FarmYearScenario(host);
        await s.RunAsync();

        // The whole catalogue (the permission-filtered one is for a real user's role).
#pragma warning disable CA1849
        var catalog = await host.InScopeAsync(sp => Task.FromResult(sp.GetRequiredService<IReportExecutionService>().GetCatalog()));
#pragma warning restore CA1849
        catalog.Count.Should().BeGreaterThan(30);

        var problems = new List<string>();
        foreach (var report in catalog)
        {
            foreach (var parameters in ParameterSets(report, s))
            {
                foreach (var language in new[] { ReportLanguage.En, ReportLanguage.Bn })
                {
                    var request = new ReportRequest(report.Key, parameters, language);
                    var label = $"{report.Key} [{language}] {string.Join(",", parameters.Select(p => $"{p.Key}={p.Value}"))}";
                    try
                    {
                        var data = await host.InScopeAsync(sp => sp.GetRequiredService<IReportExecutionService>().RunAsync(request));
                        Check(data, label, problems);
                        JsonSerializer.Serialize(data).Should().NotBeNullOrEmpty();

                        foreach (var format in new[] { ReportExportFormat.Pdf, ReportExportFormat.Xlsx })
                        {
                            var file = await host.InScopeAsync(sp => sp.GetRequiredService<IReportExecutionService>().ExportAsync(request, format));
                            if (file.Content.Length == 0)
                                problems.Add($"{label}: empty {format}");
                        }
                    }
#pragma warning disable CA1031 // The sweep collects every failure instead of stopping at the first.
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        problems.Add($"{label}: threw {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        foreach (var p in problems)
            output.WriteLine(p);
        problems.Should().BeEmpty();
    }

    private static IEnumerable<Dictionary<string, string?>> ParameterSets(ReportDescriptor report, FarmYearScenario s)
    {
        var animals = report.Parameters.Any(p => p.Type == ReportParameterType.Animal && p.Required)
            ? new[] { s.A1, s.A2, s.A3, s.A4 }
            : new[] { Guid.Empty };
        foreach (var animal in animals)
        {
            var set = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in report.Parameters)
            {
                var value = p.Type switch
                {
                    ReportParameterType.Farm => s.FarmId.ToString(),
                    ReportParameterType.DateRange => "2025-07-01..2026-06-30",
                    ReportParameterType.Date => p.Name switch
                    {
                        "fromDate" => "2025-07-01",
                        "date" => "2026-03-01",
                        _ => "2026-06-30",
                    },
                    ReportParameterType.Animal when p.Required => animal.ToString(),
                    ReportParameterType.Batch when p.Required => s.BatchId.ToString(),
                    ReportParameterType.InventoryItem => s.FeedItem.ToString(),
                    ReportParameterType.MoneyAccount => s.BankAccountId.ToString(),
                    ReportParameterType.Partner => s.PartnerB.ToString(),
                    ReportParameterType.Number when p.Name == "year" => "2026",
                    _ => p.DefaultValue,
                };
                if (value is not null)
                    set[p.Name] = value;
            }

            yield return set;
        }
    }

    private static void Check(ReportDataSet data, string label, List<string> problems)
    {
        var cells = data.Rows.SelectMany(r => r.Cells)
            .Concat(data.GrandTotals)
            .Concat(data.Groups.SelectMany(g => g.Totals));
        foreach (var cell in cells)
        {
            var f = cell.F ?? string.Empty;
            if (f.Contains("NaN", StringComparison.Ordinal) || f.Contains('∞', StringComparison.Ordinal)
                || cell.V is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                problems.Add($"{label}: NaN/∞ cell '{f}'");
            if (IsNegativeZero(f))
                problems.Add($"{label}: negative zero '{f}'");
            if (EnumNames.Contains(f.Trim()))
                problems.Add($"{label}: raw enum name '{f}'");
            if (data.Language == ReportLanguage.Bn && SingleWordEnumNames.Contains(f.Trim()))
                problems.Add($"{label}: English enum word in a Bangla report '{f}'");
        }

        foreach (var text in (data.Summary?.Figures.Select(x => x.Value) ?? []).Concat(data.Summary?.Figures.Select(x => x.Label) ?? []))
        {
            if (IsNegativeZero(text) || text.Contains("NaN", StringComparison.Ordinal))
                problems.Add($"{label}: summary figure '{text}'");
        }

        if (!data.HasGrandTotals)
            return;

        for (var i = 0; i < data.Columns.Count; i++)
        {
            // A blank total is deliberate (e.g. quantities in mixed units cannot be added up).
            if (data.Columns[i].Aggregate != ReportAggregate.Sum || i >= data.GrandTotals.Count
                || string.IsNullOrWhiteSpace(data.GrandTotals[i].F))
                continue;
            var sum = data.Rows.Sum(r => ToDecimal(r.Cells[i].V));
            var total = ToDecimal(data.GrandTotals[i].V);
            if (Math.Abs(sum - total) > 0.011m)
                problems.Add($"{label}: column '{data.Columns[i].Field}' total {total} != sum of rows {sum}");
        }
    }

    private static decimal ToDecimal(object? v) => v switch
    {
        null => 0m,
        decimal m => m,
        double d => (decimal)d,
        float f => (decimal)f,
        int n => n,
        long l => l,
        JsonElement { ValueKind: JsonValueKind.Number } e => e.GetDecimal(),
        _ => decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0m,
    };

    /// <summary>"-0", "-0.00", "(0.00)", "-৳0", "−০.০০" and the like: a zero printed with a minus sign.</summary>
    private static bool IsNegativeZero(string f)
    {
        var t = f.Trim();
        if (t.Length == 0)
            return false;
        var negative = t.StartsWith('-') || t.StartsWith('−') || (t.StartsWith('(') && t.EndsWith(')'));
        if (!negative)
            return false;
        var digits = new StringBuilder();
        foreach (var c in t)
        {
            if (char.IsDigit(c))
                digits.Append((char)('0' + (int)char.GetNumericValue(c)));
        }

        return digits.Length > 0 && digits.ToString().All(c => c == '0');
    }

    /// <summary>Multi-word enum names ("AnimalSale", "CapitalContribution") that must never reach a reader.</summary>
    private static HashSet<string> BuildEnumNames()
    {
        var types = new[]
        {
            typeof(Farm360.Domain.Finance.Enums.TransactionCategory),
            typeof(Farm360.Domain.Finance.Enums.SettlementAccount),
            typeof(Farm360.Domain.Finance.Enums.MoneyAccountKind),
            typeof(Farm360.Domain.Finance.Enums.TradeBillKind),
            typeof(Farm360.Domain.Finance.Enums.InvestorTransactionType),
            typeof(Farm360.Domain.Finance.Accounting.FixedAssetCategory),
            typeof(Farm360.Domain.Finance.Accounting.DepreciationMethod),
            typeof(Farm360.Domain.Livestock.Enums.AnimalSpecies),
            typeof(Farm360.Domain.Livestock.Enums.AnimalStatus),
            typeof(Farm360.Domain.Livestock.Enums.AcquisitionType),
            typeof(Farm360.Domain.Livestock.Enums.DisposalReason),
            typeof(Farm360.Domain.Livestock.Enums.TagType),
            typeof(Farm360.Domain.Inventory.Enums.InventoryCategory),
            typeof(Farm360.Domain.Health.Enums.CauseOfDeath),
            typeof(Farm360.Domain.Feeding.Enums.DailyFeedingEntryStatus),
        };
        return types
            .SelectMany(Enum.GetNames)
            .Where(n => n.Skip(1).Any(char.IsUpper))
            .ToHashSet(StringComparer.Ordinal);
    }
}
