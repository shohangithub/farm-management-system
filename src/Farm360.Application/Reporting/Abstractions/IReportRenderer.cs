using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Abstractions;

/// <summary>
/// Turns a <see cref="ReportDataSet"/> into a downloadable document. One implementation per
/// format, all fed from the same dataset — which is why the PDF and the spreadsheet always agree.
/// </summary>
public interface IReportRenderer
{
    ReportExportFormat Format { get; }

    string ContentType { get; }

    /// <summary>Extension without the dot, e.g. "pdf".</summary>
    string FileExtension { get; }

    /// <summary>
    /// Renders to a byte array rather than a stream: the execution service hashes the output
    /// into the <c>ReportRun</c> audit row, so the whole document has to exist anyway.
    /// Row counts are capped upstream, which keeps this bounded.
    /// </summary>
    Task<byte[]> RenderAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default);
}
