using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Reporting;

public interface IReportRunRepository
{
    void Add(ReportRun run);

    Task<ReportRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Most recent runs, newest first. <paramref name="reportKey"/> null returns all reports.</summary>
    Task<IReadOnlyList<ReportRun>> GetRecentAsync(
        string? reportKey,
        int take = 50,
        CancellationToken cancellationToken = default);
}
