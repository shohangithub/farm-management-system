using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using QuestPDF.Fluent;

namespace Farm360.Infrastructure.Reporting.Pdf;

/// <summary>
/// Renders a report to PDF with QuestPDF (SkiaSharp + HarfBuzz).
/// </summary>
/// <remarks>
/// Chosen over the browser-side html2canvas approach it replaces: that produced a bitmap of the
/// screen — unsearchable, uncopyable and several megabytes a page — purely because jsPDF cannot
/// shape Bengali. HarfBuzz shapes Bengali correctly *and* keeps the text as vectors, so this path
/// gets both. Validated by the Phase 0 spike (docs/32 §1.2).
/// </remarks>
public sealed class QuestPdfReportRenderer : IReportRenderer
{
    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public string ContentType => "application/pdf";

    public string FileExtension => "pdf";

    public Task<byte[]> RenderAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        cancellationToken.ThrowIfCancellationRequested();

        ReportFonts.EnsureRegistered();

        var bytes = new Farm360ReportDocument(dataSet).GeneratePdf();
        return Task.FromResult(bytes);
    }
}
