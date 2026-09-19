using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Registry;

/// <summary>One catalog entry — what the Report Center lists, without running anything.</summary>
public sealed record ReportDescriptor(
    string Key,
    LocalizedText Title,
    LocalizedText Description,
    ReportCategory Category,
    string? Permission,
    IReadOnlyList<ReportParameter> Parameters,
    IReadOnlyList<ReportColumn> Columns,
    PageSetup Page);

public interface IReportRegistry
{
    IReadOnlyList<ReportDescriptor> All { get; }

    bool TryGet(string key, out IReportDefinition definition);

    IReportDefinition Get(string key);

    ReportDescriptor Describe(string key);
}

/// <summary>
/// Holds every discovered report definition, keyed by <see cref="IReportDefinition.Key"/>.
/// Registered as a singleton: definitions are stateless metadata, resolved once at startup.
/// </summary>
/// <remarks>
/// Duplicate keys throw at construction rather than silently shadowing one another — a report
/// quietly replaced by another is the kind of bug that is only noticed when the printed numbers
/// are already on someone's desk.
/// </remarks>
public sealed class ReportRegistry : IReportRegistry
{
    private readonly ReadOnlyDictionary<string, IReportDefinition> _definitions;

    public ReportRegistry(IEnumerable<IReportDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var map = new Dictionary<string, IReportDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Key))
            {
                throw new InvalidOperationException(
                    $"Report definition '{definition.GetType().FullName}' has an empty Key.");
            }

            if (!map.TryAdd(definition.Key, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate report key '{definition.Key}': " +
                    $"{map[definition.Key].GetType().FullName} and {definition.GetType().FullName}.");
            }
        }

        _definitions = new ReadOnlyDictionary<string, IReportDefinition>(map);

        All = map.Values
            .Select(Describe)
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Title.En, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ReportDescriptor> All { get; }

    public bool TryGet(string key, out IReportDefinition definition) =>
        _definitions.TryGetValue(key ?? string.Empty, out definition!);

    public IReportDefinition Get(string key) =>
        TryGet(key, out var definition)
            ? definition
            : throw new ReportNotFoundException(key);

    public ReportDescriptor Describe(string key) => Describe(Get(key));

    private static ReportDescriptor Describe(IReportDefinition d) =>
        new(d.Key, d.Title, d.Description, d.Category, d.Permission, d.Parameters, d.ColumnDescriptors, d.Page);
}

public sealed class ReportNotFoundException : Exception
{
    public ReportNotFoundException(string key)
        : base($"No report is registered with key '{key}'.")
    {
        Key = key;
    }

    public ReportNotFoundException() { }

    public ReportNotFoundException(string message, Exception innerException) : base(message, innerException) { }

    public string Key { get; } = string.Empty;
}
