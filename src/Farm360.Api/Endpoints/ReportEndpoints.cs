using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Registry;
using Farm360.Application.Reporting.Services;
using Farm360.Domain.Reporting;
using Farm360.Persistence.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints;

/// <summary>Request body for running or exporting a report.</summary>
/// <remarks>
/// POST rather than GET: twelve-plus parameters do not fit a clean query string, multi-select
/// values would need escaping, and the response is not URL-cacheable in any case.
/// </remarks>
public sealed record RunReportRequest(
    Dictionary<string, string?>? Parameters,
    string? Language,
    bool BengaliNumerals = false);

public sealed record ReportCatalogItemResponse(
    string Key,
    string Title,
    string TitleBn,
    string Description,
    string Category,
    IReadOnlyList<ReportParameter> Parameters);

public sealed record ReportRunHistoryResponse(
    Guid Id,
    string ReportKey,
    string ReportTitle,
    string RequestedByName,
    DateTime RequestedAtUtc,
    string Format,
    string Outcome,
    int RowCount,
    int DurationMs,
    string? OutputHash,
    string? FailureReason);

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/reports")
            .RequireAuthorization($"Permission:{PermissionConstants.ReportsModule.View}")
            .WithTags("Reports");

        group.MapGet("/catalog", GetCatalog)
            .WithName("GetReportCatalog")
            .WithSummary("Lists every available report, grouped by module.");

        group.MapGet("/{key}/metadata", GetMetadata)
            .WithName("GetReportMetadata")
            .WithSummary("Parameter schema and column metadata for one report.");

        group.MapPost("/{key}/run", RunReport)
            .WithName("RunReport")
            .WithSummary("Executes a report and returns its dataset for the on-screen viewer.");

        // Export requires the stronger permission: reading a report on screen and walking out
        // of the building with a spreadsheet of it are different acts.
        group.MapPost("/{key}/export/{format}", ExportReport)
            .RequireAuthorization($"Permission:{PermissionConstants.ReportsModule.Export}")
            .WithName("ExportReport")
            .WithSummary("Renders a report as pdf, xlsx or csv.");

        group.MapGet("/runs", GetRunHistory)
            .WithName("GetReportRunHistory")
            .WithSummary("Audit history of report executions.");
    }

    private static IResult GetCatalog([FromServices] IReportExecutionService reports)
    {
        var items = reports.GetCatalog()
            .Select(d => new ReportCatalogItemResponse(
                d.Key,
                d.Title.En,
                d.Title.Bn,
                d.Description.En,
                d.Category.ToString(),
                d.Parameters))
            .ToList();

        return Results.Ok(items);
    }

    private static IResult GetMetadata(string key, [FromServices] IReportExecutionService reports)
    {
        try
        {
            return Results.Ok(reports.GetMetadata(key));
        }
        catch (ReportNotFoundException)
        {
            return Results.NotFound(new { message = $"No report with key '{key}'." });
        }
    }

    private static async Task<IResult> RunReport(
        string key,
        [FromBody] RunReportRequest? request,
        [FromServices] IReportExecutionService reports,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataSet = await reports.RunAsync(BuildRequest(key, request), cancellationToken);
            return Results.Ok(dataSet);
        }
        catch (ReportNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (ReportParameterException ex)
        {
            return Results.BadRequest(new { parameter = ex.ParameterName, message = ex.Message });
        }
        catch (ReportTooLargeException ex)
        {
            return Results.BadRequest(new { message = ex.Message, rowCount = ex.RowCount });
        }
        catch (ReportAccessDeniedException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> ExportReport(
        string key,
        string format,
        [FromBody] RunReportRequest? request,
        [FromServices] IReportExecutionService reports,
        CancellationToken cancellationToken)
    {
        if (!TryParseFormat(format, out var exportFormat))
        {
            return Results.BadRequest(new { message = $"Unsupported export format '{format}'. Use pdf, xlsx or csv." });
        }

        try
        {
            var rendered = await reports.ExportAsync(BuildRequest(key, request), exportFormat, cancellationToken);
            return Results.File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (ReportNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (ReportParameterException ex)
        {
            return Results.BadRequest(new { parameter = ex.ParameterName, message = ex.Message });
        }
        catch (ReportTooLargeException ex)
        {
            return Results.BadRequest(new { message = ex.Message, rowCount = ex.RowCount });
        }
        catch (ReportAccessDeniedException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> GetRunHistory(
        [FromQuery] string? reportKey,
        [FromQuery] int take,
        [FromServices] IReportRunRepository runs,
        CancellationToken cancellationToken)
    {
        var history = await runs.GetRecentAsync(reportKey, take <= 0 ? 50 : take, cancellationToken);

        return Results.Ok(history.Select(r => new ReportRunHistoryResponse(
            r.Id,
            r.ReportKey,
            r.ReportTitle,
            r.RequestedByName,
            r.RequestedAtUtc,
            r.Format.ToString(),
            r.Outcome.ToString(),
            r.RowCount,
            r.DurationMs,
            r.OutputHash,
            r.FailureReason)));
    }

    private static ReportRequest BuildRequest(string key, RunReportRequest? request)
    {
        var language = string.Equals(request?.Language, "bn", StringComparison.OrdinalIgnoreCase)
            ? ReportLanguage.Bn
            : ReportLanguage.En;

        return new ReportRequest(
            key,
            request?.Parameters ?? [],
            language,
            request?.BengaliNumerals ?? false);
    }

    private static bool TryParseFormat(string format, out ReportExportFormat result)
    {
        switch (format?.ToLowerInvariant())
        {
            case "pdf":
                result = ReportExportFormat.Pdf;
                return true;
            case "xlsx":
            case "excel":
                result = ReportExportFormat.Xlsx;
                return true;
            case "csv":
                result = ReportExportFormat.Csv;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
