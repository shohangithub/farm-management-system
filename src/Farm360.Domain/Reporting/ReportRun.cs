using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Reporting;

public enum ReportRunOutcome
{
    Succeeded = 0,
    Failed = 1,
}

public enum ReportOutputFormat
{
    Screen = 0,
    Pdf = 1,
    Xlsx = 2,
    Csv = 3,
}

/// <summary>
/// An audit record of one report execution.
/// </summary>
/// <remarks>
/// Reports are not reproducible by re-running them: feed cost is a weighted average that moves,
/// market prices change, and the valuation policy can be re-configured. A report printed in
/// March and re-run in September will legitimately differ, and without this row neither run can
/// prove what it said. Recording the parameters, the rates in force and a hash of the rendered
/// bytes is what makes the pack audit-grade and lets the Accounts Head sign it.
/// See docs/32 §4 GAP-4.
/// </remarks>
public sealed class ReportRun : AuditableEntity
{
    private ReportRun() { } // EF Core

    public string ReportKey { get; private set; } = string.Empty;

    public string ReportTitle { get; private set; } = string.Empty;

    /// <summary>Bound parameters as JSON, exactly as supplied — the report's reproduction recipe.</summary>
    public string ParametersJson { get; private set; } = "{}";

    /// <summary>
    /// Rates and policies in force when this ran (meat price, feed weighted-average cost,
    /// valuation policy). Stored as JSON because the set differs per report and grows over time.
    /// </summary>
    public string? RatesSnapshotJson { get; private set; }

    public Guid? RequestedByUserId { get; private set; }

    public string RequestedByName { get; private set; } = string.Empty;

    public DateTime RequestedAtUtc { get; private set; }

    public ReportOutputFormat Format { get; private set; }

    public ReportRunOutcome Outcome { get; private set; }

    public int RowCount { get; private set; }

    public int DurationMs { get; private set; }

    /// <summary>SHA-256 of the rendered bytes. Null for on-screen runs, which produce no file.</summary>
    public string? OutputHash { get; private set; }

    public long? OutputBytes { get; private set; }

    /// <summary>Blob key when the rendered file is archived (financial reports). Null otherwise.</summary>
    public string? ArchivedBlobKey { get; private set; }

    public string? FailureReason { get; private set; }

    public static ReportRun Start(
        Guid tenantId,
        string reportKey,
        string reportTitle,
        string parametersJson,
        ReportOutputFormat format,
        Guid? requestedByUserId,
        string requestedByName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportKey);

        var run = new ReportRun
        {
            Id = Guid.NewGuid(),
            ReportKey = reportKey,
            ReportTitle = reportTitle ?? string.Empty,
            ParametersJson = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson,
            Format = format,
            RequestedByUserId = requestedByUserId,
            RequestedByName = string.IsNullOrWhiteSpace(requestedByName) ? "unknown" : requestedByName,
            RequestedAtUtc = DateTime.UtcNow,
            Outcome = ReportRunOutcome.Succeeded,
        };

        run.SetTenantId(tenantId);
        return run;
    }

    public void Complete(int rowCount, int durationMs, string? outputHash = null, long? outputBytes = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);

        Outcome = ReportRunOutcome.Succeeded;
        RowCount = rowCount;
        DurationMs = durationMs < 0 ? 0 : durationMs;
        OutputHash = outputHash;
        OutputBytes = outputBytes;
    }

    public void Fail(string reason, int durationMs)
    {
        Outcome = ReportRunOutcome.Failed;
        DurationMs = durationMs < 0 ? 0 : durationMs;

        // Truncated: this is an audit breadcrumb, not a log sink.
        FailureReason = reason is { Length: > 2000 } ? reason[..2000] : reason;
    }

    public void AttachRatesSnapshot(string ratesJson) => RatesSnapshotJson = ratesJson;

    public void MarkArchived(string blobKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobKey);
        ArchivedBlobKey = blobKey;
    }
}
